// Copyright (c) Rivoli AI 2026. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Net.Http.Json;
using Andy.Settings.Application.Messaging;
using Andy.Settings.Application.Messaging.Events;
using Andy.Settings.Domain.Entities;
using Andy.Settings.Domain.Enums;
using Andy.Settings.Infrastructure.Data;
using Andy.Settings.Infrastructure.Messaging;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Andy.Settings.Tests.Integration.Messaging;

// End-to-end coverage for AL3 / AL4: a value write through the public
// REST surface lands an OutboxEntry in the same transaction with the
// expected subject + payload. The OutboxDispatcher then drains the row
// and the in-memory bus delivers it on the consumer-shaped wildcard.
public class OutboxEmissionTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public OutboxEmissionTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Setting_a_value_writes_outbox_row_with_app_scoped_subject()
    {
        // Pick a seeded definition so we don't have to set one up first.
        // The test fixture's DataSeeder loads several registrations
        // under Fixtures/registrations/*.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SettingsDbContext>();
        var definition = await db.SettingDefinitions.FirstAsync();

        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/values", new
        {
            definitionKey = definition.Key,
            scopeType = ScopeType.Application,
            scopeId = definition.ApplicationCode,
            valueJson = "\"hello\"",
        });
        resp.EnsureSuccessStatusCode();

        // Reload the context — the API request used its own scope.
        // Client-side ordering: SQLite EF can't ORDER BY DateTimeOffset
        // directly, and this is one row per test so paying for a
        // ToListAsync first is cheap and portable.
        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<SettingsDbContext>();
        var allRows = await verifyDb.Outbox.ToListAsync();
        var row = allRows.OrderByDescending(o => o.CreatedAt).First();

        row.Subject.Should().Be(
            $"andy.settings.events.config.{definition.ApplicationCode}.created",
            because: "first write to a scope is a creation event under AL3's taxonomy");
        row.PayloadJson.Should().Contain("\"key\":")
            .And.Contain($"\"{definition.Key}\"",
                because: "the payload's `key` field must echo the setting's full key for consumer lookup");
        row.PayloadJson.Should().Contain("new_value_digest",
            because: "consumers detect re-delivery by comparing the digest");
    }

    [Fact]
    public async Task Updating_a_value_emits_updated_subject()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SettingsDbContext>();
        var definition = await db.SettingDefinitions.FirstAsync();

        var client = _factory.CreateClient();
        var first = await client.PostAsJsonAsync("/api/values", new
        {
            definitionKey = definition.Key,
            scopeType = ScopeType.User,
            scopeId = "test-user-emit",
            valueJson = "\"v1\"",
        });
        first.EnsureSuccessStatusCode();

        var second = await client.PostAsJsonAsync("/api/values", new
        {
            definitionKey = definition.Key,
            scopeType = ScopeType.User,
            scopeId = "test-user-emit",
            valueJson = "\"v2\"",
        });
        second.EnsureSuccessStatusCode();

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<SettingsDbContext>();
        var allRows = await verifyDb.Outbox.ToListAsync();
        var updateRow = allRows
            .Where(o => o.Subject.EndsWith(".updated"))
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefault();

        updateRow.Should().NotBeNull(because: "the second write is an update, not a create");
        updateRow!.Subject.Should().Be(
            $"andy.settings.events.config.{definition.ApplicationCode}.updated");
    }

    [Fact]
    public async Task OutboxDispatcher_publishes_row_and_marks_it_dispatched()
    {
        // Subscribe before writing — the in-memory bus drops messages
        // with no matching subscriber, and the BackgroundService polls
        // aggressively (50ms in tests) so the publish can land before
        // we'd otherwise call SubscribeAsync.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SettingsDbContext>();
        var bus = (InMemoryMessageBus)scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var definition = await db.SettingDefinitions.FirstAsync();

        const string filter = "andy.settings.events.config.>";
        bus.EnsureChannel(filter);

        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/values", new
        {
            definitionKey = definition.Key,
            scopeType = ScopeType.Workspace,
            scopeId = "ws-dispatch-test",
            valueJson = "\"dispatched\"",
        });
        resp.EnsureSuccessStatusCode();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        ConfigChangedPayload? received = null;
        try
        {
            await foreach (var msg in bus.SubscribeAsync(filter,
                new SubscriptionOptions("test-dispatch"), cts.Token))
            {
                var payload = msg.Deserialize<ConfigChangedPayload>();
                if (payload is not null && payload.ScopeId == "ws-dispatch-test")
                {
                    received = payload;
                    break;
                }
            }
        }
        catch (OperationCanceledException) { /* timeout */ }

        received.Should().NotBeNull(
            because: "the dispatcher must drain the outbox row and publish to the bus within the poll interval");
        received!.ApplicationCode.Should().Be(definition.ApplicationCode);
        received.Key.Should().Be(definition.Key);
        received.NewValueDigest.Should().NotBeNullOrEmpty();

        // And the published-at sentinel must be stamped or the
        // dispatcher would re-publish the row on the next tick.
        using var afterScope = _factory.Services.CreateScope();
        var afterDb = afterScope.ServiceProvider.GetRequiredService<SettingsDbContext>();
        var allAfter = await afterDb.Outbox.ToListAsync();
        var dispatched = allAfter
            .Where(o => o.PayloadJson.Contains("ws-dispatch-test"))
            .FirstOrDefault();
        dispatched.Should().NotBeNull();
        dispatched!.PublishedAt.Should().NotBeNull(
            because: "a successful publish must stamp PublishedAt");
        dispatched.AttemptCount.Should().Be(0);
    }
}
