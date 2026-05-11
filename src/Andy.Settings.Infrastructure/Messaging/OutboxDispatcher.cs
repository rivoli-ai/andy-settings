// Copyright (c) Rivoli AI 2026. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;
using Andy.Settings.Application.Messaging;
using Andy.Settings.Domain.Entities;
using Andy.Settings.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Andy.Settings.Infrastructure.Messaging;

// Background worker that drains the OutboxEntry table to IMessageBus.
// One instance per service. Polls at a configurable interval, batches
// pending rows, publishes each to its target subject, records success
// or failure. Rows are never deleted — the outbox doubles as an audit
// log; a separate retention job may purge published rows older than
// N days.
//
// Retry semantics: on publish failure the row stays pending with
// AttemptCount incremented and LastError set. The dispatcher respects
// an exponential-backoff delay (OutboxDispatcherOptions.BackoffBase *
// 2^(AttemptCount-1), capped at BackoffMax) before reconsidering the
// row so a poison message does not spin the worker at full speed.
public sealed class OutboxDispatcher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxDispatcher> _logger;
    private readonly OutboxDispatcherOptions _options;

    public OutboxDispatcher(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxDispatcher> logger,
        IOptions<OutboxDispatcherOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;

        // AK2: warn (don't throw) when configured PollInterval exceeds
        // the cap so misconfigured production deployments are visible
        // in logs without failing boot.
        if (_options.PollInterval > OutboxDispatcherOptions.MaxRecommendedPollInterval)
        {
            _logger.LogWarning(
                "Messaging:Outbox:PollInterval is {Configured}, exceeds AK2 cap of {Max}. " +
                "Config-change events will be delayed by up to that interval.",
                _options.PollInterval, OutboxDispatcherOptions.MaxRecommendedPollInterval);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxDispatcher started (poll={Poll}, batch={Batch})",
            _options.PollInterval, _options.BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var drained = await DrainOnceAsync(stoppingToken);
                if (drained == 0)
                {
                    await Task.Delay(_options.PollInterval, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OutboxDispatcher tick failed; backing off");
                await Task.Delay(_options.PollInterval, stoppingToken);
            }
        }

        _logger.LogInformation("OutboxDispatcher stopped");
    }

    // Drains one batch. Public-internal so integration tests can invoke
    // a single iteration deterministically instead of waiting on the
    // background loop's cadence.
    internal async Task<int> DrainOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SettingsDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var now = DateTimeOffset.UtcNow;

        // Fetch unordered then sort in-memory. SQLite (used in the
        // Conductor-embedded deployment) refuses to ORDER BY
        // DateTimeOffset server-side; the batch cap bounds the working
        // set so the client-side sort is cheap. Take(BatchSize * 2) so
        // we don't miss freshly-inserted older rows under a steady
        // burst — still bounded.
        var rawPending = await db.Set<OutboxEntry>()
            .Where(e => e.PublishedAt == null)
            .Take(_options.BatchSize * 2)
            .ToListAsync(ct);
        var pending = rawPending
            .OrderBy(e => e.CreatedAt)
            .Take(_options.BatchSize)
            .ToList();

        if (pending.Count == 0)
        {
            return 0;
        }

        var eligible = pending.Where(e => IsEligible(e, now)).ToList();
        if (eligible.Count == 0)
        {
            return 0;
        }

        foreach (var entry in eligible)
        {
            try
            {
                var headers = new MessageHeaders(
                    MsgId: entry.Id,
                    CorrelationId: entry.CorrelationId,
                    CausationId: entry.CausationId,
                    Generation: entry.Generation);

                // Payload is stored as JSON text, but IMessageBus.PublishAsync
                // takes an object (it re-serializes). JsonDocument lets the
                // bus re-serialize verbatim without a typed round-trip.
                using var doc = JsonDocument.Parse(entry.PayloadJson);
                await bus.PublishAsync(entry.Subject, doc.RootElement, headers, ct);

                entry.PublishedAt = DateTimeOffset.UtcNow;
                entry.LastError = null;
            }
            catch (Exception ex)
            {
                entry.AttemptCount++;
                entry.LastAttemptAt = DateTimeOffset.UtcNow;
                entry.LastError = ex.Message;
                _logger.LogWarning(ex,
                    "Outbox entry {EntryId} publish failed (attempt {Attempt})",
                    entry.Id, entry.AttemptCount);
            }
        }

        await db.SaveChangesAsync(ct);
        return eligible.Count;
    }

    private bool IsEligible(OutboxEntry entry, DateTimeOffset now)
    {
        if (entry.AttemptCount == 0 || entry.LastAttemptAt is null)
        {
            return true;
        }

        var factor = Math.Pow(2, entry.AttemptCount - 1);
        var delayTicks = (long)(_options.BackoffBase.Ticks * factor);
        var delay = TimeSpan.FromTicks(Math.Min(delayTicks, _options.BackoffMax.Ticks));
        return now - entry.LastAttemptAt.Value >= delay;
    }
}
