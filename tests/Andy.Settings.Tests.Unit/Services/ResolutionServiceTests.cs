using Andy.Settings.Application.DTOs.Effective;
using Andy.Settings.Domain.Entities;
using Andy.Settings.Domain.Enums;
using Andy.Settings.Infrastructure.Data;
using Andy.Settings.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Andy.Settings.Tests.Unit.Services;

public class ResolutionServiceTests : IDisposable
{
    private readonly SettingsDbContext _db;
    private readonly ResolutionService _sut;

    public ResolutionServiceTests()
    {
        var options = new DbContextOptionsBuilder<SettingsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new SettingsDbContext(options);
        _sut = new ResolutionService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    private async Task<SettingDefinition> SeedDefinition(
        string key = "app.test.key",
        string? defaultValueJson = "\"default-value\"",
        bool isSecret = false,
        SettingDataType dataType = SettingDataType.String)
    {
        var definition = new SettingDefinition
        {
            Id = Guid.NewGuid(),
            Key = key,
            ApplicationCode = "testapp",
            DisplayName = "Test Setting",
            DataType = dataType,
            DefaultValueJson = defaultValueJson,
            IsSecret = isSecret,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _db.SettingDefinitions.Add(definition);
        await _db.SaveChangesAsync();
        return definition;
    }

    private async Task<SettingAssignment> SeedAssignment(
        Guid definitionId,
        ScopeType scopeType,
        string? scopeId,
        string valueJson)
    {
        var assignment = new SettingAssignment
        {
            Id = Guid.NewGuid(),
            DefinitionId = definitionId,
            ScopeType = scopeType,
            ScopeId = scopeId,
            ValueJson = valueJson,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _db.SettingAssignments.Add(assignment);
        await _db.SaveChangesAsync();
        return assignment;
    }

    [Fact]
    public async Task Resolve_NoAssignment_ReturnsDefault()
    {
        await SeedDefinition();

        var context = new ResolutionContext { UserId = "user1" };
        var result = await _sut.ResolveAsync("app.test.key", context);

        result.IsValid.Should().BeTrue();
        result.IsDefault.Should().BeTrue();
        result.EffectiveValue.Should().Be("\"default-value\"");
        result.Key.Should().Be("app.test.key");
    }

    [Fact]
    public async Task Resolve_MachineScope_OverridesDefault()
    {
        var def = await SeedDefinition();
        await SeedAssignment(def.Id, ScopeType.Machine, null, "\"machine-value\"");

        var context = new ResolutionContext { UserId = "user1" };
        var result = await _sut.ResolveAsync("app.test.key", context);

        result.IsDefault.Should().BeFalse();
        result.EffectiveValue.Should().Be("\"machine-value\"");
        result.WinningScopeType.Should().Be(ScopeType.Machine);
    }

    [Fact]
    public async Task Resolve_UserScope_OverridesMachine()
    {
        var def = await SeedDefinition();
        await SeedAssignment(def.Id, ScopeType.Machine, null, "\"machine-value\"");
        await SeedAssignment(def.Id, ScopeType.User, "user1", "\"user-value\"");

        var context = new ResolutionContext { UserId = "user1" };
        var result = await _sut.ResolveAsync("app.test.key", context);

        result.IsDefault.Should().BeFalse();
        result.EffectiveValue.Should().Be("\"user-value\"");
        result.WinningScopeType.Should().Be(ScopeType.User);
    }

    [Fact]
    public async Task Resolve_TeamScope_OverridesUser()
    {
        var def = await SeedDefinition();
        await SeedAssignment(def.Id, ScopeType.User, "user1", "\"user-value\"");
        await SeedAssignment(def.Id, ScopeType.Team, "team1", "\"team-value\"");

        var context = new ResolutionContext { UserId = "user1", TeamId = "team1" };
        var result = await _sut.ResolveAsync("app.test.key", context);

        result.IsDefault.Should().BeFalse();
        result.EffectiveValue.Should().Be("\"team-value\"");
        result.WinningScopeType.Should().Be(ScopeType.Team);
    }

    [Fact]
    public async Task Resolve_RuntimeOverride_WinsOverAll()
    {
        var def = await SeedDefinition();
        await SeedAssignment(def.Id, ScopeType.Machine, null, "\"machine-value\"");
        await SeedAssignment(def.Id, ScopeType.User, "user1", "\"user-value\"");
        await SeedAssignment(def.Id, ScopeType.Team, "team1", "\"team-value\"");
        await SeedAssignment(def.Id, ScopeType.Workspace, "ws1", "\"workspace-value\"");
        await SeedAssignment(def.Id, ScopeType.RuntimeOverride, "user1", "\"override-value\"");

        var context = new ResolutionContext
        {
            UserId = "user1",
            TeamId = "team1",
            WorkspaceId = "ws1"
        };
        var result = await _sut.ResolveAsync("app.test.key", context);

        result.IsDefault.Should().BeFalse();
        result.EffectiveValue.Should().Be("\"override-value\"");
        result.WinningScopeType.Should().Be(ScopeType.RuntimeOverride);
    }

    [Fact]
    public async Task Explain_ReturnsSourceChainWithWinnerMarked()
    {
        var def = await SeedDefinition();
        await SeedAssignment(def.Id, ScopeType.Machine, null, "\"machine-value\"");
        await SeedAssignment(def.Id, ScopeType.User, "user1", "\"user-value\"");

        var context = new ResolutionContext { UserId = "user1" };
        var result = await _sut.ExplainAsync("app.test.key", context);

        result.SourceChain.Should().NotBeEmpty();
        result.SourceChain.Should().Contain(e => e.IsWinner);

        var winner = result.SourceChain.Single(e => e.IsWinner);
        winner.ScopeType.Should().Be(ScopeType.User);
        winner.ValueJson.Should().Be("\"user-value\"");
    }

    [Fact]
    public async Task ResolveBatch_HandlesMultipleKeys()
    {
        var def1 = await SeedDefinition("app.key1", "\"default1\"");
        var def2 = await SeedDefinition("app.key2", "\"default2\"");
        await SeedAssignment(def1.Id, ScopeType.Machine, null, "\"machine1\"");

        var context = new ResolutionContext { UserId = "user1" };
        var results = await _sut.ResolveBatchAsync(new[] { "app.key1", "app.key2" }, context);

        results.Should().HaveCount(2);
        results[0].Key.Should().Be("app.key1");
        results[0].EffectiveValue.Should().Be("\"machine1\"");
        results[1].Key.Should().Be("app.key2");
        results[1].EffectiveValue.Should().Be("\"default2\"");
        results[1].IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task Resolve_SecretDefinition_ReturnsNullEffectiveValue()
    {
        var def = await SeedDefinition(isSecret: true);
        await SeedAssignment(def.Id, ScopeType.Machine, null, "\"secret-value\"");

        var context = new ResolutionContext { UserId = "user1" };
        var result = await _sut.ResolveAsync("app.test.key", context);

        result.EffectiveValue.Should().BeNull();
        result.IsSecret.Should().BeTrue();
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Resolve_DefinitionNotFound_ReturnsInvalid()
    {
        var context = new ResolutionContext { UserId = "user1" };
        var result = await _sut.ResolveAsync("nonexistent.key", context);

        result.IsValid.Should().BeFalse();
        result.ValidationMessage.Should().Contain("not found");
        result.Key.Should().Be("nonexistent.key");
    }
}
