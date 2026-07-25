// Copyright (c) Rivoli AI 2026. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Andy.Settings.Application.Messaging;
using Andy.Settings.Domain.Entities;
using Andy.Settings.Infrastructure.Data;
using Andy.Settings.Infrastructure.Messaging;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Andy.Settings.Tests.Unit.Messaging;

// rivoli-ai/andy-settings#139. The dispatcher fetched a batch with NO server-
// side ORDER BY and had no terminal state for a row that can never publish. So
// once permanently-failing rows exceeded the fetch window they filled every
// batch and NEW events were never published — config changes silently stopped
// propagating to every consumer, with only repeated log warnings as a signal.
public class OutboxDeadLetterTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SettingsDbContext _db;
    private readonly ServiceProvider _services;

    private sealed class AlwaysFailsBus : IMessageBus
    {
        public Task PublishAsync(string subject, object payload, MessageHeaders headers, CancellationToken ct = default)
            => throw new InvalidOperationException("bus is down");

        public IAsyncEnumerable<IncomingMessage> SubscribeAsync(
            string subjectFilter, SubscriptionOptions options, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class RecordingBus : IMessageBus
    {
        public List<string> Published { get; } = [];

        public Task PublishAsync(string subject, object payload, MessageHeaders headers, CancellationToken ct = default)
        {
            Published.Add(subject);
            return Task.CompletedTask;
        }

        public IAsyncEnumerable<IncomingMessage> SubscribeAsync(
            string subjectFilter, SubscriptionOptions options, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private readonly RecordingBus _recording = new();

    public OutboxDeadLetterTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _db = new SettingsDbContext(
            new DbContextOptionsBuilder<SettingsDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();

        var services = new ServiceCollection();
        services.AddSingleton(_db);
        services.AddSingleton<IMessageBus>(_recording);
        _services = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _services.Dispose();
        _db.Dispose();
        _connection.Dispose();
    }

    private OutboxDispatcher Dispatcher(IMessageBus bus, int maxAttempts = 3, int batchSize = 5)
    {
        var services = new ServiceCollection();
        services.AddSingleton(_db);
        services.AddSingleton(bus);
        var provider = services.BuildServiceProvider();

        return new OutboxDispatcher(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OutboxDispatcher>.Instance,
            Options.Create(new OutboxDispatcherOptions
            {
                MaxAttempts = maxAttempts,
                BatchSize = batchSize,
                BackoffBase = TimeSpan.Zero,
                BackoffMax = TimeSpan.Zero,
            }));
    }

    private async Task<OutboxEntry> AddEntry(string subject, DateTimeOffset createdAt, int attemptCount = 0)
    {
        var entry = new OutboxEntry
        {
            Id = Guid.NewGuid(),
            Subject = subject,
            PayloadJson = "{}",
            CorrelationId = Guid.NewGuid(),
            CreatedAt = createdAt,
            AttemptCount = attemptCount,
        };
        _db.Outbox.Add(entry);
        await _db.SaveChangesAsync();
        return entry;
    }

    [Fact]
    public async Task DrainOnce_RowExceedingMaxAttempts_IsNotRetried()
    {
        await AddEntry("andy.settings.events.config.a.updated", DateTimeOffset.UtcNow, attemptCount: 3);

        var drained = await Dispatcher(_recording, maxAttempts: 3).DrainOnceAsync(CancellationToken.None);

        drained.Should().Be(0);
        _recording.Published.Should().BeEmpty();
    }

    // The starvation scenario: more poison rows than fit in one batch, plus one
    // healthy row created later. Before the fix the healthy row never
    // published.
    [Fact]
    public async Task DrainOnce_PoisonBacklogLargerThanBatch_DoesNotStarveNewEvents()
    {
        var start = DateTimeOffset.UtcNow.AddHours(-1);
        for (var i = 0; i < 20; i++)
            await AddEntry($"poison.{i}", start.AddSeconds(i), attemptCount: 3);

        await AddEntry("healthy.new", DateTimeOffset.UtcNow);

        var drained = await Dispatcher(_recording, maxAttempts: 3, batchSize: 5)
            .DrainOnceAsync(CancellationToken.None);

        drained.Should().Be(1);
        _recording.Published.Should().ContainSingle().Which.Should().Be("healthy.new");
    }

    [Fact]
    public async Task DrainOnce_FailingRow_ReachesDeadLetterAndStopsBeingRetried()
    {
        await AddEntry("andy.settings.events.config.a.updated", DateTimeOffset.UtcNow);
        var dispatcher = Dispatcher(new AlwaysFailsBus(), maxAttempts: 3);

        for (var i = 0; i < 5; i++)
            await dispatcher.DrainOnceAsync(CancellationToken.None);

        _db.ChangeTracker.Clear();
        var entry = await _db.Outbox.SingleAsync();
        entry.AttemptCount.Should().Be(3, "retries stop once MaxAttempts is reached");
        entry.PublishedAt.Should().BeNull();
        entry.LastError.Should().Be("bus is down");
    }

    // Rows are never deleted — the outbox doubles as an audit log, and an
    // operator requeues by resetting AttemptCount.
    [Fact]
    public async Task DrainOnce_DeadLetteredRow_RemainsInTableAndCanBeRequeued()
    {
        var entry = await AddEntry("andy.settings.events.config.a.updated", DateTimeOffset.UtcNow, attemptCount: 3);

        (await _db.Outbox.CountAsync()).Should().Be(1);

        entry.AttemptCount = 0;
        await _db.SaveChangesAsync();

        var drained = await Dispatcher(_recording, maxAttempts: 3).DrainOnceAsync(CancellationToken.None);

        drained.Should().Be(1);
        _recording.Published.Should().ContainSingle();
    }

    [Fact]
    public async Task DrainOnce_OrdersByCreatedAtServerSide()
    {
        var now = DateTimeOffset.UtcNow;
        await AddEntry("third", now);
        await AddEntry("first", now.AddMinutes(-10));
        await AddEntry("second", now.AddMinutes(-5));

        await Dispatcher(_recording, batchSize: 2).DrainOnceAsync(CancellationToken.None);

        _recording.Published.Should().Equal("first", "second");
    }

    [Fact]
    public async Task DrainOnce_OverlongExceptionMessage_IsTruncatedToColumnWidth()
    {
        await AddEntry("andy.settings.events.config.a.updated", DateTimeOffset.UtcNow);

        var dispatcher = new OutboxDispatcher(
            BuildScopeFactory(new ThrowsLongMessageBus()),
            NullLogger<OutboxDispatcher>.Instance,
            Options.Create(new OutboxDispatcherOptions
            {
                MaxAttempts = 5, BatchSize = 5,
                BackoffBase = TimeSpan.Zero, BackoffMax = TimeSpan.Zero,
            }));

        await dispatcher.DrainOnceAsync(CancellationToken.None);

        _db.ChangeTracker.Clear();
        (await _db.Outbox.SingleAsync()).LastError!.Length.Should().Be(2000);
    }

    private sealed class ThrowsLongMessageBus : IMessageBus
    {
        public Task PublishAsync(string subject, object payload, MessageHeaders headers, CancellationToken ct = default)
            => throw new InvalidOperationException(new string('x', 5000));

        public IAsyncEnumerable<IncomingMessage> SubscribeAsync(
            string subjectFilter, SubscriptionOptions options, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private IServiceScopeFactory BuildScopeFactory(IMessageBus bus)
    {
        var services = new ServiceCollection();
        services.AddSingleton(_db);
        services.AddSingleton(bus);
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }
}
