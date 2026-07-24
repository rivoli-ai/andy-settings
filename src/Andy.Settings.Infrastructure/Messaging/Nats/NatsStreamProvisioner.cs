// Copyright (c) Rivoli AI 2026. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

namespace Andy.Settings.Infrastructure.Messaging.Nats;

// Ensures the JetStream stream exists before any BackgroundService
// (OutboxDispatcher, future consumers) starts publishing or
// subscribing. IHostedService.StartAsync runs before BackgroundService
// .ExecuteAsync, so the ordering guarantee is built into the host.
//
// ANDY_DOMAIN is SHARED — andy-tasks, andy-issues, andy-agents and andy-rbac
// all carry subjects on it. `StreamConfig.Subjects` is the stream's COMPLETE
// subject list, so a blind CreateOrUpdate with only our subjects would delete
// everyone else's routing. This service therefore only ever ADDS: it reads the
// existing config, unions its required subjects into it, and skips the write
// entirely when they are already covered (rivoli-ai/andy-settings#149).
//
// Per AK5: stream retention window is logged at startup so operators
// can verify the configured class without grepping config.
public sealed class NatsStreamProvisioner(
    NatsMessageBus bus,
    IOptions<NatsOptions> options,
    ILogger<NatsStreamProvisioner> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        await bus.ConnectAsync(ct);

        var opts = options.Value;
        var required = RequiredSubjects(opts);
        var existing = await TryGetExistingSubjectsAsync(opts.StreamName, ct);

        if (existing is null)
        {
            await bus.JetStream.CreateOrUpdateStreamAsync(
                new StreamConfig(opts.StreamName, required) { MaxAge = opts.MaxAge }, ct);

            logger.LogInformation(
                "NATS JetStream stream {Stream} created with subjects [{Subjects}] retention {MaxAge}",
                opts.StreamName, string.Join(", ", required), opts.MaxAge);
            return;
        }

        // Coverage, not equality. JetStream rejects a subject list containing
        // overlapping patterns, and the shared ANDY_DOMAIN already carries
        // `andy.*.dlq.>` — which covers this service's `andy.settings.dlq.>`.
        // Adding it would be redundant and the whole update would be refused.
        var missing = required
            .Where(subject => !existing.Any(e => NatsSubject.Covers(e, subject)))
            .ToArray();

        if (missing.Length == 0)
        {
            logger.LogInformation(
                "NATS JetStream stream {Stream} already covers subjects [{Subjects}] retention {MaxAge}; no update needed",
                opts.StreamName, string.Join(", ", required), opts.MaxAge);
            return;
        }

        // Refuse to broaden over somebody else's subject rather than silently
        // swallowing their traffic into our slice of a shared stream.
        var wouldSubsume = missing
            .SelectMany(subject => existing
                .Where(e => NatsSubject.Covers(subject, e))
                .Select(e => (Ours: subject, Theirs: e)))
            .ToArray();

        if (wouldSubsume.Length > 0)
        {
            throw new InvalidOperationException(
                $"Refusing to provision stream '{opts.StreamName}': configured subject(s) " +
                string.Join(", ", wouldSubsume.Select(x => $"'{x.Ours}' would subsume existing '{x.Theirs}'")) +
                ". Narrow Messaging:Nats:StreamSubjects, or give this service its own stream.");
        }

        // Union, never replace — preserves every other service's subjects.
        var merged = existing.Concat(missing).ToArray();

        await bus.JetStream.CreateOrUpdateStreamAsync(
            new StreamConfig(opts.StreamName, merged) { MaxAge = opts.MaxAge }, ct);

        logger.LogInformation(
            "NATS JetStream stream {Stream} extended with [{Added}]; full subject set is now [{Subjects}] retention {MaxAge}",
            opts.StreamName, string.Join(", ", missing), string.Join(", ", merged), opts.MaxAge);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    /// <summary>
    /// The subjects this service needs on the stream: its configured event
    /// subjects plus its DLQ prefix.
    /// </summary>
    /// <remarks>
    /// The deduplication here is load-bearing, not defensive.
    /// <see cref="NatsOptions.StreamSubjects"/> carries a non-empty default and
    /// the .NET configuration binder APPENDS bound array elements to a
    /// property's existing array rather than replacing it. Because
    /// appsettings.json ships the same value the default already holds, the
    /// bound result was that subject twice — which NATS rejects with
    /// "duplicate subjects detected", killing the host on every boot under the
    /// shipped configuration.
    ///
    /// The DLQ prefix was previously absent from the subject list even though
    /// <c>NatsMessageBus</c> publishes dead letters to
    /// <c>{DlqPrefix}.{originalSubject}</c>. That only worked by accident,
    /// because the shared ANDY_DOMAIN happened to carry <c>andy.*.dlq.&gt;</c>.
    /// </remarks>
    internal static string[] RequiredSubjects(NatsOptions opts)
    {
        var configured = opts.StreamSubjects.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
        var subjects = (configured.Length > 0 ? configured : NatsOptions.DefaultStreamSubjects)
            .AsEnumerable();

        if (!string.IsNullOrWhiteSpace(opts.DlqPrefix))
            subjects = subjects.Append($"{opts.DlqPrefix.TrimEnd('.')}.>");

        return subjects
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Current subject list for the stream, or null when it does not exist yet.
    /// </summary>
    private async Task<string[]?> TryGetExistingSubjectsAsync(string streamName, CancellationToken ct)
    {
        try
        {
            var stream = await bus.JetStream.GetStreamAsync(streamName, cancellationToken: ct);
            return stream.Info.Config.Subjects?.ToArray() ?? [];
        }
        catch (NatsJSApiNoResponseException)
        {
            throw;
        }
        catch (NatsJSApiException ex) when (ex.Error.Code == 404)
        {
            return null;
        }
    }
}
