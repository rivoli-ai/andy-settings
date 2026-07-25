using Andy.Settings.Application.DTOs.Audit;
using Andy.Settings.Application.Interfaces;
using Andy.Settings.Domain.Entities;
using Andy.Settings.Domain.Enums;
using Andy.Settings.Infrastructure.Data;
using Andy.Settings.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Andy.Settings.Tests.Unit.Services;

// rivoli-ai/andy-settings#135. Definition mutations emitted no outbox events
// and no audit rows. Deletion is the sharp case: it cascades to every stored
// assignment AND every encrypted secret, and did so in total silence —
// consumers invalidate on events, so they kept serving values that no longer
// existed, indefinitely.
//
// Real SQLite rather than the InMemory provider, because the cascade is
// enforced by the database.
public class DefinitionCascadeEventTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SettingsDbContext _db;
    private readonly Mock<IAuditService> _audit = new();
    private readonly DefinitionRepository _sut;

    public DefinitionCascadeEventTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _db = new SettingsDbContext(
            new DbContextOptionsBuilder<SettingsDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();

        _audit.Setup(a => a.RecordAsync(It.IsAny<AuditEventDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new DefinitionRepository(_db, _audit.Object);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task<SettingDefinition> SeedWithStoredValues(bool withSecret)
    {
        var definition = new SettingDefinition
        {
            Id = Guid.NewGuid(),
            Key = "app.cascade.key",
            ApplicationCode = "testapp",
            DisplayName = "Cascade",
            DataType = withSecret ? SettingDataType.Secret : SettingDataType.String,
            IsSecret = withSecret,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        _db.SettingDefinitions.Add(definition);

        if (withSecret)
        {
            _db.EncryptedSecrets.Add(new EncryptedSecret
            {
                Id = Guid.NewGuid(), DefinitionId = definition.Id, ScopeType = ScopeType.Machine,
                ScopeId = null, EncryptedValue = "ciphertext",
                CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
            });
        }
        else
        {
            foreach (var (scope, scopeId) in new[]
                     {
                         (ScopeType.Machine, (string?)null),
                         (ScopeType.User, "user1"),
                     })
            {
                _db.SettingAssignments.Add(new SettingAssignment
                {
                    Id = Guid.NewGuid(), DefinitionId = definition.Id, ScopeType = scope,
                    ScopeId = scopeId, ValueJson = "\"v\"", Etag = "e", Version = 1,
                    CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
                });
            }
        }

        await _db.SaveChangesAsync();
        return definition;
    }

    [Fact]
    public async Task DeleteAsync_EmitsOneDeletedEventPerCascadedAssignment()
    {
        await SeedWithStoredValues(withSecret: false);

        await _sut.DeleteAsync("app.cascade.key", "deleter");

        var events = await _db.Outbox.ToListAsync();
        events.Should().HaveCount(2, "each cascade-deleted assignment must be announced");
        events.Should().OnlyContain(e => e.Subject.EndsWith(".deleted"));
        events.Should().OnlyContain(e => e.Subject.Contains("testapp"));
    }

    [Fact]
    public async Task DeleteAsync_WithStoredSecrets_EmitsSecretDeletedEvent()
    {
        await SeedWithStoredValues(withSecret: true);

        await _sut.DeleteAsync("app.cascade.key", "deleter");

        var events = await _db.Outbox.ToListAsync();
        events.Should().ContainSingle()
            .Which.PayloadJson.Should().Contain("app.cascade.key");
    }

    [Fact]
    public async Task DeleteAsync_RecordsAuditWithActor()
    {
        await SeedWithStoredValues(withSecret: false);

        await _sut.DeleteAsync("app.cascade.key", "deleter");

        _audit.Verify(a => a.RecordAsync(
            It.Is<AuditEventDto>(e =>
                e.EventType == AuditEventType.Deleted &&
                e.DefinitionKey == "app.cascade.key" &&
                e.ActorId == "deleter"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // The events must not outlive a rolled-back delete: they go into the same
    // SaveChanges as the removal (ADR 0001 §3).
    [Fact]
    public async Task DeleteAsync_CascadeAndEventsCommitTogether()
    {
        await SeedWithStoredValues(withSecret: false);

        await _sut.DeleteAsync("app.cascade.key", "deleter");

        (await _db.SettingDefinitions.CountAsync()).Should().Be(0);
        (await _db.SettingAssignments.CountAsync()).Should().Be(0);
        (await _db.Outbox.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task DeleteAsync_NoStoredValues_StillAuditsButEmitsNoEvents()
    {
        _db.SettingDefinitions.Add(new SettingDefinition
        {
            Id = Guid.NewGuid(), Key = "app.bare.key", ApplicationCode = "testapp",
            DisplayName = "Bare", DataType = SettingDataType.String,
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();

        await _sut.DeleteAsync("app.bare.key", "deleter");

        (await _db.Outbox.CountAsync()).Should().Be(0, "nothing was stored, so nothing went stale");
        _audit.Verify(a => a.RecordAsync(
            It.Is<AuditEventDto>(e => e.EventType == AuditEventType.Deleted),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
