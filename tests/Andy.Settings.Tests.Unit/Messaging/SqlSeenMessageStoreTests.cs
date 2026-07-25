// Copyright (c) Rivoli AI 2026. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Andy.Settings.Domain.Entities;
using Andy.Settings.Infrastructure.Data;
using Andy.Settings.Infrastructure.Messaging;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace Andy.Settings.Tests.Unit.Messaging;

// rivoli-ai/andy-settings#137. A lost dedup race left the failed insert in the
// Added state in the scoped change tracker, so the next SaveChangesAsync on
// that context — the consumer's own unrelated write, in the same message scope
// — re-attempted the duplicate insert and threw again.
//
// Runs against real SQLite: the unique constraint has to actually fire, and the
// InMemory provider does not enforce one.
public class SqlSeenMessageStoreTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SettingsDbContext _db;
    private readonly SqlSeenMessageStore _sut;

    public SqlSeenMessageStoreTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<SettingsDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new SettingsDbContext(options);
        _db.Database.EnsureCreated();
        _sut = new SqlSeenMessageStore(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private static readonly TimeSpan Ttl = TimeSpan.FromDays(1);

    [Fact]
    public async Task TryMarkSeenAsync_FirstDelivery_ReturnsTrue()
    {
        var result = await _sut.TryMarkSeenAsync(Guid.NewGuid(), "andy.settings.events.config.x.updated", Ttl);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task TryMarkSeenAsync_Redelivery_ReturnsFalse()
    {
        var msgId = Guid.NewGuid();
        await _sut.TryMarkSeenAsync(msgId, "s", Ttl);

        var second = await _sut.TryMarkSeenAsync(msgId, "s", Ttl);

        second.Should().BeFalse();
    }

    // The regression: after losing the race, an unrelated write on the same
    // scoped context must still succeed.
    //
    // Losing the race means the competing row appears strictly BETWEEN the
    // store's existence check and its SaveChangesAsync. Inserting it beforehand
    // is not the same test — the existence check short-circuits and the
    // Add/catch path never runs. A SaveChanges interceptor is what makes the
    // window deterministic.
    [Fact]
    public async Task TryMarkSeenAsync_AfterLostRace_LeavesChangeTrackerClean()
    {
        var msgId = Guid.NewGuid();
        var connection = _connection;

        await using var racy = new SettingsDbContext(
            new DbContextOptionsBuilder<SettingsDbContext>()
                .UseSqlite(_connection)
                .AddInterceptors(new InsertCompetingRowInterceptor(connection, msgId))
                .Options);
        var sut = new SqlSeenMessageStore(racy);

        var result = await sut.TryMarkSeenAsync(msgId, "s", Ttl);

        result.Should().BeFalse();
        racy.ChangeTracker.Entries<SeenMessage>()
            .Should().NotContain(e => e.State == EntityState.Added);

        // The consumer's own follow-up write on the same scope must not inherit
        // the poisoned entity. Without the detach this throws the same unique
        // violation and fails an operation that has nothing to do with dedup.
        racy.SettingDefinitions.Add(new SettingDefinition
        {
            Id = Guid.NewGuid(),
            Key = "after.lost.race",
            ApplicationCode = "testapp",
            DisplayName = "After lost race",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

        var save = async () => await racy.SaveChangesAsync();
        await save.Should().NotThrowAsync();
    }

    // Inserts the competing SeenMessage row on a separate connection during
    // SavingChanges — i.e. after the store's existence check has already
    // returned "not seen" — so the store's own insert loses the race.
    private sealed class InsertCompetingRowInterceptor(SqliteConnection connection, Guid msgId)
        : SaveChangesInterceptor
    {
        private bool _fired;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (!_fired)
            {
                _fired = true;

                // Insert through EF on the same connection so the Guid key and
                // the DateTimeOffset columns use the identical storage mapping.
                // A hand-written INSERT does not conflict: EF's Guid-to-TEXT
                // format differs from Guid.ToString(), so the row lands under a
                // different primary key and the race never happens.
                using var competitor = new SettingsDbContext(
                    new DbContextOptionsBuilder<SettingsDbContext>().UseSqlite(connection).Options);
                competitor.Set<SeenMessage>().Add(new SeenMessage
                {
                    MsgId = msgId,
                    Subject = "s",
                    SeenAt = DateTimeOffset.UtcNow,
                    ExpiresAt = DateTimeOffset.UtcNow.Add(Ttl),
                });
                competitor.SaveChanges();
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
