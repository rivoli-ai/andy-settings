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

// GetCategoriesAsync replaced an approximation: the MCP settings_categories
// tool fetched a 1000-definition page and reduced it client-side, which
// under-reported once the catalog outgrew that page. The paging cap introduced
// with #134 lowered the truncation point to 500, so the approximation had to be
// removed rather than re-tuned.
public class DefinitionCategoriesTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SettingsDbContext _db;
    private readonly DefinitionRepository _sut;

    public DefinitionCategoriesTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _db = new SettingsDbContext(
            new DbContextOptionsBuilder<SettingsDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();

        var audit = new Mock<IAuditService>();
        audit.Setup(a => a.RecordAsync(It.IsAny<AuditEventDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new DefinitionRepository(_db, audit.Object);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private void Seed(string key, string? category, string applicationCode = "testapp")
    {
        _db.SettingDefinitions.Add(new SettingDefinition
        {
            Id = Guid.NewGuid(), Key = key, ApplicationCode = applicationCode,
            DisplayName = key, DataType = SettingDataType.String, Category = category,
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
        });
    }

    [Fact]
    public async Task GetCategoriesAsync_ReturnsDistinctSortedNonEmpty()
    {
        Seed("a", "UI");
        Seed("b", "Security");
        Seed("c", "UI");
        Seed("d", null);
        Seed("e", "");
        await _db.SaveChangesAsync();

        var categories = await _sut.GetCategoriesAsync();

        categories.Should().Equal("Security", "UI");
    }

    [Fact]
    public async Task GetCategoriesAsync_FiltersByApplicationCode()
    {
        Seed("a", "UI", "app-one");
        Seed("b", "Security", "app-two");
        await _db.SaveChangesAsync();

        (await _sut.GetCategoriesAsync("app-one")).Should().Equal("UI");
    }

    // The regression guard: a catalog larger than any page size must still
    // yield every category.
    [Fact]
    public async Task GetCategoriesAsync_CompleteBeyondTheMaxPageSize()
    {
        for (var i = 0; i < 1200; i++)
            Seed($"key.{i}", $"cat-{i:D4}");
        await _db.SaveChangesAsync();

        var categories = await _sut.GetCategoriesAsync();

        categories.Should().HaveCount(1200);
        categories.Should().Contain("cat-1199", "the last category must survive, not be cut off at a page boundary");
    }
}
