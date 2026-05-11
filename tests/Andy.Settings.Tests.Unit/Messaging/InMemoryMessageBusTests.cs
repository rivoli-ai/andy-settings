// Copyright (c) Rivoli AI 2026. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;
using Andy.Settings.Application.Messaging;
using Andy.Settings.Infrastructure.Messaging;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Andy.Settings.Tests.Unit.Messaging;

public class InMemoryMessageBusTests
{
    [Theory]
    [InlineData("andy.settings.events.config.mcp-gateway.updated", "andy.settings.events.config.mcp-gateway.updated", true)]
    [InlineData("andy.settings.events.config.mcp-gateway.>", "andy.settings.events.config.mcp-gateway.updated", true)]
    [InlineData("andy.settings.events.config.*.updated", "andy.settings.events.config.mcp-gateway.updated", true)]
    [InlineData("andy.settings.events.config.mcp-gateway.>", "andy.settings.events.config.andy-containers.updated", false)]
    [InlineData("andy.settings.events.config.*.created", "andy.settings.events.config.mcp-gateway.updated", false)]
    [InlineData("andy.settings.events.config.>", "andy.settings.events.config", false)]
    public void MatchesSubject_follows_NATS_wildcard_semantics(string filter, string subject, bool expected)
    {
        // ADR 0001 §4: '*' matches one token; '>' matches one or more
        // trailing tokens. The in-memory bus implements the same
        // semantics as JetStream so tests written against InMemory port
        // unchanged to NATS.
        InMemoryMessageBus.MatchesSubject(filter, subject).Should().Be(expected);
    }

    [Fact]
    public async Task PublishAsync_drops_messages_past_generation_limit()
    {
        // Cycle-breaker contract from ADR 0001 §5. The bus drops the
        // message before any subscriber sees it — verified by ensuring
        // a registered subscription never receives a payload.
        var bus = new InMemoryMessageBus(NullLogger<InMemoryMessageBus>.Instance);
        var doomed = new MessageHeaders(Guid.NewGuid(), Guid.NewGuid(), null,
            Generation: MessageHeaders.MaxGeneration + 1);

        bus.EnsureChannel("andy.>");

        await bus.PublishAsync("andy.settings.events.config.x.updated", new { }, doomed);

        // Subscribe with a short timeout. If the message got through we
        // would receive it within milliseconds; the timeout firing
        // means the drop happened correctly.
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        var sub = bus.SubscribeAsync("andy.>", new SubscriptionOptions("test-durable"), cts.Token);

        var received = 0;
        try
        {
            await foreach (var _ in sub.WithCancellation(cts.Token))
            {
                received++;
            }
        }
        catch (OperationCanceledException) { /* expected */ }

        received.Should().Be(0);
    }

    [Fact]
    public async Task PublishAsync_round_trips_payload_with_snake_case_keys()
    {
        // Contract test for the EventJson wire format. A drift between
        // publisher and consumer serialization would silently
        // deserialize fields to defaults; pinning the convention here
        // catches that early.
        var bus = new InMemoryMessageBus(NullLogger<InMemoryMessageBus>.Instance);
        bus.EnsureChannel("test.subject");

        var headers = MessageHeaders.NewRoot();
        var payload = new { SchemaVersion = 1, ApplicationCode = "mcp-gateway" };
        await bus.PublishAsync("test.subject", payload, headers);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await foreach (var msg in bus.SubscribeAsync("test.subject",
            new SubscriptionOptions("test-durable"), cts.Token))
        {
            var json = System.Text.Encoding.UTF8.GetString(msg.Payload.Span);
            using var doc = JsonDocument.Parse(json);
            doc.RootElement.TryGetProperty("schema_version", out _).Should().BeTrue(
                because: "EventJson must serialize PascalCase properties as snake_case for cross-service compatibility");
            doc.RootElement.TryGetProperty("application_code", out _).Should().BeTrue();
            return;
        }

        throw new Exception("subscription closed before any message arrived");
    }
}
