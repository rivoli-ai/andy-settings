using System.Text.Json;
using Andy.Settings.Api.Data;
using Andy.Settings.Domain.Entities;
using Andy.Settings.Domain.Enums;
using Andy.Settings.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Andy.Settings.Tests.Unit.Api;

// rivoli-ai/andy-settings#136. The seeder was insert-if-absent, which made
// registration manifests WRITE-ONCE: once a key existed, changing its default,
// validation, allowed scopes or description in the owning service's manifest
// had no effect ever again. Since manifests are the distribution mechanism for
// definitions, every definition in the ecosystem was frozen at whatever shape
// it had on first boot.
//
// Agreed ownership rule: the manifest wins for SCHEMA; stored VALUES are never
// touched. Definitions that vanish are deprecated, not deleted.
public class DataSeederReconcileTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SettingsDbContext _db;
    private readonly string _manifestDir;

    public DataSeederReconcileTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _db = new SettingsDbContext(
            new DbContextOptionsBuilder<SettingsDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();

        _manifestDir = Path.Combine(Path.GetTempPath(), "andy-settings-seeder-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_manifestDir);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
        if (Directory.Exists(_manifestDir)) Directory.Delete(_manifestDir, recursive: true);
    }

    private void WriteManifest(string serviceName, params object[] definitions)
    {
        var manifest = new
        {
            service = new { name = serviceName },
            settings = new { definitions },
        };
        File.WriteAllText(
            Path.Combine(_manifestDir, $"{serviceName}.json"),
            JsonSerializer.Serialize(manifest));
    }

    private DataSeeder Seeder()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Registrations:ManifestPaths:0"] = _manifestDir,
            })
            .Build();
        return new DataSeeder(_db, configuration, NullLogger<DataSeeder>.Instance);
    }

    [Fact]
    public async Task SeedAsync_NewDefinition_IsInserted()
    {
        WriteManifest("testapp", new { key = "app.k", displayName = "K", dataType = "String" });

        await Seeder().SeedAsync();

        var stored = await _db.SettingDefinitions.SingleAsync();
        stored.Key.Should().Be("app.k");
        stored.ApplicationCode.Should().Be("testapp");
    }

    // The regression: a manifest edit must actually roll out.
    [Fact]
    public async Task SeedAsync_ChangedManifest_UpdatesExistingDefinition()
    {
        WriteManifest("testapp", new
        {
            key = "app.k", displayName = "Old", description = "before",
            dataType = "String", defaultValue = "a",
        });
        await Seeder().SeedAsync();

        WriteManifest("testapp", new
        {
            key = "app.k", displayName = "New", description = "after",
            dataType = "String", defaultValue = "b",
        });
        await Seeder().SeedAsync();

        var stored = await _db.SettingDefinitions.SingleAsync();
        stored.DisplayName.Should().Be("New");
        stored.Description.Should().Be("after");
        stored.DefaultValueJson.Should().Be("\"b\"");
    }

    [Fact]
    public async Task SeedAsync_UnchangedManifest_IsIdempotent()
    {
        WriteManifest("testapp", new { key = "app.k", displayName = "K", dataType = "String" });
        await Seeder().SeedAsync();
        var first = (await _db.SettingDefinitions.SingleAsync()).UpdatedAt;

        await Seeder().SeedAsync();

        var stored = await _db.SettingDefinitions.SingleAsync();
        stored.UpdatedAt.Should().Be(first, "an unchanged manifest must not churn the row");
    }

    // Manifest owns the schema; the operator's stored values are not the
    // seeder's business.
    [Fact]
    public async Task SeedAsync_ManifestWins_ButStoredValuesSurvive()
    {
        WriteManifest("testapp", new { key = "app.k", displayName = "K", dataType = "String", defaultValue = "a" });
        await Seeder().SeedAsync();

        var definition = await _db.SettingDefinitions.SingleAsync();
        _db.SettingAssignments.Add(new SettingAssignment
        {
            Id = Guid.NewGuid(), DefinitionId = definition.Id, ScopeType = ScopeType.Machine,
            ScopeId = null, ValueJson = "\"operator-set\"", Etag = "e", Version = 1,
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
        });
        // An operator edits the schema through the API.
        definition.DisplayName = "Operator Edit";
        await _db.SaveChangesAsync();

        WriteManifest("testapp", new { key = "app.k", displayName = "K", dataType = "String", defaultValue = "a" });
        await Seeder().SeedAsync();

        _db.ChangeTracker.Clear();
        (await _db.SettingDefinitions.SingleAsync()).DisplayName
            .Should().Be("K", "the manifest owns the schema");
        (await _db.SettingAssignments.SingleAsync()).ValueJson
            .Should().Be("\"operator-set\"", "stored values are never touched by the seeder");
    }

    [Fact]
    public async Task SeedAsync_DefinitionRemovedFromManifest_IsDeprecatedNotDeleted()
    {
        WriteManifest("testapp",
            new { key = "app.keep", displayName = "Keep", dataType = "String" },
            new { key = "app.drop", displayName = "Drop", dataType = "String" });
        await Seeder().SeedAsync();

        WriteManifest("testapp", new { key = "app.keep", displayName = "Keep", dataType = "String" });
        await Seeder().SeedAsync();

        _db.ChangeTracker.Clear();
        var dropped = await _db.SettingDefinitions.SingleAsync(d => d.Key == "app.drop");
        dropped.IsDeprecated.Should().BeTrue();
        (await _db.SettingDefinitions.CountAsync()).Should().Be(2, "nothing is deleted");
    }

    [Fact]
    public async Task SeedAsync_DefinitionReturnsToManifest_IsUnDeprecated()
    {
        WriteManifest("testapp", new { key = "app.k", displayName = "K", dataType = "String" });
        await Seeder().SeedAsync();
        WriteManifest("testapp");
        await Seeder().SeedAsync();

        WriteManifest("testapp", new { key = "app.k", displayName = "K", dataType = "String" });
        await Seeder().SeedAsync();

        _db.ChangeTracker.Clear();
        (await _db.SettingDefinitions.SingleAsync()).IsDeprecated.Should().BeFalse();
    }

    // The dangerous case: a manifest that fails to load must not read as
    // "everything for that service disappeared".
    [Fact]
    public async Task SeedAsync_ManifestMissing_DoesNotDeprecateItsDefinitions()
    {
        WriteManifest("testapp", new { key = "app.k", displayName = "K", dataType = "String" });
        await Seeder().SeedAsync();

        File.Delete(Path.Combine(_manifestDir, "testapp.json"));
        await Seeder().SeedAsync();

        _db.ChangeTracker.Clear();
        (await _db.SettingDefinitions.SingleAsync()).IsDeprecated
            .Should().BeFalse("a failed manifest load is not a declaration of removal");
    }

    [Fact]
    public async Task SeedAsync_UnparseableManifest_DoesNotDeprecateItsDefinitions()
    {
        WriteManifest("testapp", new { key = "app.k", displayName = "K", dataType = "String" });
        await Seeder().SeedAsync();

        File.WriteAllText(Path.Combine(_manifestDir, "testapp.json"), "{ not json");
        await Seeder().SeedAsync();

        _db.ChangeTracker.Clear();
        (await _db.SettingDefinitions.SingleAsync()).IsDeprecated.Should().BeFalse();
    }

    [Fact]
    public async Task SeedAsync_OtherServicesDefinitions_AreLeftAlone()
    {
        _db.SettingDefinitions.Add(new SettingDefinition
        {
            Id = Guid.NewGuid(), Key = "other.k", ApplicationCode = "otherapp",
            DisplayName = "Other", DataType = SettingDataType.String,
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();

        WriteManifest("testapp", new { key = "app.k", displayName = "K", dataType = "String" });
        await Seeder().SeedAsync();

        _db.ChangeTracker.Clear();
        var other = await _db.SettingDefinitions.SingleAsync(d => d.Key == "other.k");
        other.IsDeprecated.Should().BeFalse("no manifest was loaded for otherapp");
    }

    // Switching storage mode with values stored would strand or expose them.
    [Fact]
    public async Task SeedAsync_SecretModeSwitchWithStoredValues_IsRefused()
    {
        WriteManifest("testapp", new { key = "app.k", displayName = "K", dataType = "String" });
        await Seeder().SeedAsync();

        var definition = await _db.SettingDefinitions.SingleAsync();
        _db.SettingAssignments.Add(new SettingAssignment
        {
            Id = Guid.NewGuid(), DefinitionId = definition.Id, ScopeType = ScopeType.Machine,
            ScopeId = null, ValueJson = "\"v\"", Etag = "e", Version = 1,
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();

        WriteManifest("testapp", new { key = "app.k", displayName = "K", dataType = "Secret" });
        await Seeder().SeedAsync();

        _db.ChangeTracker.Clear();
        var stored = await _db.SettingDefinitions.SingleAsync();
        stored.DataType.Should().Be(SettingDataType.String);
        stored.IsSecret.Should().BeFalse();
    }
}
