// Copyright (c) Rivoli AI 2026. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Andy.Settings.Application.Messaging;
using Andy.Settings.Application.Messaging.Events;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Andy.Settings.Infrastructure.Messaging.Consumers;

// Template for a service that consumes `andy.settings.events.config.*`
// events. NOT registered in andy-settings' own DI — per ADR 0001 §4,
// a service must never subscribe to its own namespace. The class lives
// here as a reference shape; downstream services (mcp-gateway, future
// audit pipelines, etc.) extend it.
//
// To onboard a new consumer:
//   1. Reference `IMessageBus` from your own DI (the publisher's
//      contract is stable across NATS / InMemory).
//   2. Extend this class with a concrete handler method.
//   3. Register as a hosted service in your Program.cs.
//
// The class respects every AK invariant:
//   - AK3: dedupes on msg-id via the consumer's SeenMessages table
//          (caller-provided so each service owns its own dedup state).
//   - AK4: consumer runs unconditionally — operational pause is done
//          on the NATS server (`nats consumer pause`), not via a
//          configuration flag.
//   - Generation-limit overflow + DLQ are handled by NatsMessageBus
//     before delivery, so the handler never sees a doomed message.
public abstract class ConfigEventConsumerBase : BackgroundService
{
    // Subscribe wildcards: subclasses pick e.g.
    //   "andy.settings.events.config.mcp-gateway.>"
    // to receive only their slice, or
    //   "andy.settings.events.config.>"
    // to receive all config changes.
    protected abstract string SubjectFilter { get; }

    // Stable durable consumer name. JetStream keys consumer offsets by
    // this name; reuse on reconnect resumes from the last acked sequence.
    protected abstract string DurableName { get; }

    private readonly IMessageBus _bus;
    private readonly ILogger _logger;

    protected ConfigEventConsumerBase(IMessageBus bus, ILogger logger)
    {
        _bus = bus;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Config-event consumer {Name} subscribing on {Filter}",
            DurableName, SubjectFilter);

        var options = new SubscriptionOptions(DurableName: DurableName);

        await foreach (var msg in _bus.SubscribeAsync(SubjectFilter, options, stoppingToken))
        {
            try
            {
                if (await IsDuplicateAsync(msg, stoppingToken))
                {
                    _logger.LogDebug("Skipping duplicate msg-id {MsgId} on {Subject}",
                        msg.Headers.MsgId, msg.Subject);
                    await msg.AckAsync(stoppingToken);
                    continue;
                }

                var payload = msg.Deserialize<ConfigChangedPayload>();
                if (payload is null)
                {
                    _logger.LogWarning(
                        "Empty or malformed config event on {Subject} msg-id {MsgId}",
                        msg.Subject, msg.Headers.MsgId);
                    await msg.AckAsync(stoppingToken);
                    continue;
                }

                await HandleAsync(payload, msg.Headers, stoppingToken);
                await msg.AckAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Config-event handler {Name} threw on {Subject} msg-id {MsgId}; nacking for redelivery",
                    DurableName, msg.Subject, msg.Headers.MsgId);
                await msg.NackAsync(stoppingToken);
            }
        }
    }

    // Concrete subclasses dedupe by inserting the msg-id into their own
    // SeenMessages table (or equivalent). The base class doesn't take a
    // dependency on andy-settings' SqlSeenMessageStore because consumers
    // in other services have their own DbContext + table.
    protected abstract Task<bool> IsDuplicateAsync(IncomingMessage msg, CancellationToken ct);

    // The interesting bit. Implement whatever invalidation / cache-bust
    // / re-fetch logic the consumer needs. The payload's NewValueDigest
    // distinguishes a real change from a re-delivery; consumers that
    // need the authoritative value should GET it from andy-settings'
    // REST API.
    protected abstract Task HandleAsync(
        ConfigChangedPayload payload,
        MessageHeaders headers,
        CancellationToken ct);
}
