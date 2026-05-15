// Copyright (c) Rivoli AI 2026. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Andy.Settings.Domain.Entities;
using Andy.Settings.Infrastructure.Data;
using Andy.Settings.Infrastructure.Messaging;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Andy.Settings.Tests.Unit.Messaging;

// Regression coverage for the SQLite `DateTimeOffset` translation bug
// that caused `SeenMessagesCleanupJob` to throw on every tick under the
// Conductor-embedded provider:
//
//   System.InvalidOperationException: The LINQ expression
//   'DbSet<SeenMessage>().Where(s => s.ExpiresAt < __now_0)'
//   could not be translated.
//
// Reproduces against a real SQLite in-memory database — the
// InMemory provider used by the rest of the unit suite does not exhibit
// the bug because it does not translate to SQL at all. Without this
// test the regression silently returns the next time a developer adds
// a new entity or rewrites the convention.
//
// Mirrors the andy-rbac#74 / andy-agents#174 fix.
public class SeenMessagesCleanupSqliteTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SettingsDbContext _db;

    public SeenMessagesCleanupSqliteTests()
    {
        // Open connection up front and hold it for the lifetime of the
        // test — closing it would drop the in-memory database.
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<SettingsDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new SettingsDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task PurgeExpired_TranslatesUnderSqlite()
    {
        // Before the convention fix this call threw
        // `InvalidOperationException: The LINQ expression ... could not
        // be translated` — the exact symptom that filled
        // andy-settings.log every PurgeInterval tick.
        var store = new SqlSeenMessageStore(_db);

        var act = () => store.PurgeExpiredAsync(batchSize: 100);

        await act.Should().NotThrowAsync(
            "the DateTimeOffset convention must let `Where(s => s.ExpiresAt < now)` translate to SQL under SQLite");
    }

    [Fact]
    public async Task PurgeExpired_RemovesOnlyExpiredRows()
    {
        var now = DateTimeOffset.UtcNow;
        _db.SeenMessages.AddRange(
            new SeenMessage
            {
                MsgId = Guid.NewGuid(),
                Subject = "test.subject",
                SeenAt = now - TimeSpan.FromHours(2),
                ExpiresAt = now - TimeSpan.FromHours(1), // expired
            },
            new SeenMessage
            {
                MsgId = Guid.NewGuid(),
                Subject = "test.subject",
                SeenAt = now - TimeSpan.FromHours(2),
                ExpiresAt = now - TimeSpan.FromMinutes(1), // expired
            },
            new SeenMessage
            {
                MsgId = Guid.NewGuid(),
                Subject = "test.subject",
                SeenAt = now,
                ExpiresAt = now + TimeSpan.FromHours(1), // live
            });
        await _db.SaveChangesAsync();

        var store = new SqlSeenMessageStore(_db);
        var purged = await store.PurgeExpiredAsync(batchSize: 100);

        purged.Should().Be(2, "two rows had ExpiresAt < now");
        var remaining = await _db.SeenMessages.ToListAsync();
        remaining.Should().HaveCount(1,
            "the unexpired row must survive — the comparison was working numerically, not lexicographically");
        remaining[0].ExpiresAt.Should().BeAfter(now);
    }

    [Fact]
    public async Task TryMarkSeen_ThenPurge_RoundTripsUnderSqlite()
    {
        // End-to-end smoke: the path the consumer actually uses
        // (TryMarkSeenAsync writes a row with DateTimeOffset values;
        // PurgeExpiredAsync later compares against now). Both legs must
        // translate under SQLite or the dedup story falls over the
        // moment the embedded provider is selected.
        var store = new SqlSeenMessageStore(_db);

        var firstSeen = await store.TryMarkSeenAsync(
            msgId: Guid.NewGuid(),
            subject: "test.subject",
            ttl: TimeSpan.FromMilliseconds(1));
        firstSeen.Should().BeTrue();

        // Wait past the TTL so the row is purgeable.
        await Task.Delay(50);

        var purged = await store.PurgeExpiredAsync(batchSize: 100);
        purged.Should().Be(1);
    }
}
