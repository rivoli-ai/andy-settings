using Andy.Settings.Application.DTOs.Audit;
using Andy.Settings.Domain.Entities;
using Andy.Settings.Domain.Enums;
using Andy.Settings.Infrastructure.Data;
using Andy.Settings.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Andy.Settings.Tests.Unit.Services;

public class AuditRepositoryTests : IDisposable
{
    private readonly SettingsDbContext _db;
    private readonly AuditRepository _repo;

    public AuditRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<SettingsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new SettingsDbContext(options);
        _repo = new AuditRepository(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task SeedAuditEvents()
    {
        var now = DateTimeOffset.UtcNow;
        var events = new[]
        {
            new AuditEvent { Id = Guid.NewGuid(), EventType = AuditEventType.Created, DefinitionKey = "andy.containers.defaultProvider", ScopeType = ScopeType.Application, ScopeId = "containers", ActorId = "user-1", CreatedAt = now.AddMinutes(-5) },
            new AuditEvent { Id = Guid.NewGuid(), EventType = AuditEventType.Updated, DefinitionKey = "andy.containers.defaultProvider", ScopeType = ScopeType.User, ScopeId = "sami", ActorId = "user-1", CreatedAt = now.AddMinutes(-3) },
            new AuditEvent { Id = Guid.NewGuid(), EventType = AuditEventType.Created, DefinitionKey = "andy.codeindex.embedding.model", ScopeType = ScopeType.Application, ScopeId = "codeindex", ActorId = "user-2", CreatedAt = now.AddMinutes(-2) },
            new AuditEvent { Id = Guid.NewGuid(), EventType = AuditEventType.Deleted, DefinitionKey = "andy.containers.maxCpuCores", ScopeType = ScopeType.User, ScopeId = "sami", ActorId = "user-1", CreatedAt = now.AddMinutes(-1) },
            new AuditEvent { Id = Guid.NewGuid(), EventType = AuditEventType.SecretRotated, DefinitionKey = "andy.codeindex.embedding.apiKey", ScopeType = ScopeType.Machine, ActorId = "user-2", CreatedAt = now },
        };
        _db.AuditEvents.AddRange(events);
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task QueryAsync_ReturnsAllEvents_WhenNoFilters()
    {
        await SeedAuditEvents();

        var result = await _repo.QueryAsync(new AuditQuery());

        result.TotalCount.Should().Be(5);
        result.Items.Should().HaveCount(5);
    }

    [Fact]
    public async Task QueryAsync_OrdersByCreatedAtDescending()
    {
        await SeedAuditEvents();

        var result = await _repo.QueryAsync(new AuditQuery());

        result.Items[0].EventType.Should().Be(AuditEventType.SecretRotated); // most recent
        result.Items[^1].EventType.Should().Be(AuditEventType.Created);       // oldest
    }

    [Fact]
    public async Task QueryAsync_FiltersByDefinitionKey()
    {
        await SeedAuditEvents();

        var result = await _repo.QueryAsync(new AuditQuery { DefinitionKey = "andy.containers.defaultProvider" });

        result.TotalCount.Should().Be(2);
        result.Items.Should().OnlyContain(e => e.DefinitionKey == "andy.containers.defaultProvider");
    }

    [Fact]
    public async Task QueryAsync_FiltersByActorId()
    {
        await SeedAuditEvents();

        var result = await _repo.QueryAsync(new AuditQuery { ActorId = "user-2" });

        result.TotalCount.Should().Be(2);
        result.Items.Should().OnlyContain(e => e.ActorId == "user-2");
    }

    [Fact]
    public async Task QueryAsync_FiltersByEventType()
    {
        await SeedAuditEvents();

        var result = await _repo.QueryAsync(new AuditQuery { EventType = AuditEventType.Created });

        result.TotalCount.Should().Be(2);
        result.Items.Should().OnlyContain(e => e.EventType == AuditEventType.Created);
    }

    [Fact]
    public async Task QueryAsync_FiltersByDateRange()
    {
        await SeedAuditEvents();
        var now = DateTimeOffset.UtcNow;

        var result = await _repo.QueryAsync(new AuditQuery
        {
            DateFrom = now.AddMinutes(-3),
            DateTo = now.AddMinutes(-1)
        });

        result.TotalCount.Should().Be(2); // Updated (-3m) and Created (-2m), Deleted is at -1m boundary
    }

    [Fact]
    public async Task QueryAsync_Paginates()
    {
        await SeedAuditEvents();

        var page1 = await _repo.QueryAsync(new AuditQuery { Page = 1, PageSize = 2 });
        var page2 = await _repo.QueryAsync(new AuditQuery { Page = 2, PageSize = 2 });

        page1.TotalCount.Should().Be(5);
        page1.Items.Should().HaveCount(2);
        page2.Items.Should().HaveCount(2);
        page1.Items[0].Id.Should().NotBe(page2.Items[0].Id);
    }

    [Fact]
    public async Task RecordAsync_CreatesAuditEvent()
    {
        var dto = new AuditEventDto(
            Guid.NewGuid(), AuditEventType.Created, "test.key",
            ScopeType.Machine, null, "User", "user-1",
            null, "\"value\"", null, DateTimeOffset.UtcNow);

        await _repo.RecordAsync(dto);

        var events = await _db.AuditEvents.ToListAsync();
        events.Should().HaveCount(1);
        events[0].DefinitionKey.Should().Be("test.key");
        events[0].EventType.Should().Be(AuditEventType.Created);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsEvent_WhenFound()
    {
        await SeedAuditEvents();
        var first = await _db.AuditEvents.FirstAsync();

        var result = await _repo.GetByIdAsync(first.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(first.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        var result = await _repo.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task QueryAsync_CombinesFilters()
    {
        await SeedAuditEvents();

        var result = await _repo.QueryAsync(new AuditQuery
        {
            DefinitionKey = "andy.containers.defaultProvider",
            ActorId = "user-1",
            EventType = AuditEventType.Updated
        });

        result.TotalCount.Should().Be(1);
        result.Items[0].DefinitionKey.Should().Be("andy.containers.defaultProvider");
        result.Items[0].EventType.Should().Be(AuditEventType.Updated);
    }
}
