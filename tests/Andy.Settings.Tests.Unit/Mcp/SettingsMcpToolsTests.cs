using System.Text.Json;
using Andy.Settings.Api.Mcp;
using Andy.Settings.Application.DTOs.Audit;
using Andy.Settings.Application.DTOs.Common;
using Andy.Settings.Application.DTOs.Definitions;
using Andy.Settings.Application.DTOs.Effective;
using Andy.Settings.Application.DTOs.ImportExport;
using Andy.Settings.Application.Interfaces;
using Andy.Settings.Domain.Enums;
using FluentAssertions;
using Moq;

namespace Andy.Settings.Tests.Unit.Mcp;

public class SettingsMcpToolsTests
{
    private readonly Mock<IDefinitionService> _definitionService = new();
    private readonly Mock<IResolutionService> _resolutionService = new();
    private readonly Mock<IAssignmentService> _assignmentService = new();
    private readonly Mock<IAuditService> _auditService = new();
    private readonly Mock<IExportImportService> _exportImportService = new();
    private readonly Mock<ISecretService> _secretService = new();
    private readonly SettingsMcpTools _sut;

    public SettingsMcpToolsTests()
    {
        _sut = new SettingsMcpTools(
            _definitionService.Object,
            _resolutionService.Object,
            _assignmentService.Object,
            _auditService.Object,
            _exportImportService.Object,
            _secretService.Object);
    }

    [Fact]
    public async Task ListDefinitions_ReturnsJsonWithDefinitions()
    {
        // Arrange
        var definitions = new PagedResult<DefinitionDto>(
            Items: new[]
            {
                new DefinitionDto(
                    Id: Guid.NewGuid(),
                    Key: "app.theme",
                    ApplicationCode: "testapp",
                    DisplayName: "Theme",
                    Description: "UI theme",
                    Category: "UI",
                    DataType: SettingDataType.String,
                    DefaultValueJson: "\"light\"",
                    ValidationJson: null,
                    UiSchemaJson: null,
                    IsSecret: false,
                    AllowedScopesJson: null,
                    TagsJson: null,
                    IsDeprecated: false,
                    CreatedAt: DateTimeOffset.UtcNow,
                    UpdatedAt: DateTimeOffset.UtcNow,
                    AssignmentCount: 0),
            },
            TotalCount: 1,
            Page: 1,
            PageSize: 25);

        _definitionService
            .Setup(s => s.SearchAsync(It.IsAny<DefinitionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(definitions);

        // Act
        var json = await _sut.ListDefinitions();

        // Assert
        json.Should().Contain("app.theme");
        json.Should().Contain("totalCount");
        json.Should().Contain("items");

        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("items").GetArrayLength().Should().Be(1);
        doc.RootElement.GetProperty("totalCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Search_WithQuery_ReturnsMatchingResults()
    {
        // Arrange
        var definitions = new PagedResult<DefinitionDto>(
            Items: new[]
            {
                new DefinitionDto(
                    Id: Guid.NewGuid(),
                    Key: "app.theme.color",
                    ApplicationCode: "testapp",
                    DisplayName: "Theme Color",
                    Description: "Primary theme color",
                    Category: "UI",
                    DataType: SettingDataType.String,
                    DefaultValueJson: "\"blue\"",
                    ValidationJson: null,
                    UiSchemaJson: null,
                    IsSecret: false,
                    AllowedScopesJson: null,
                    TagsJson: null,
                    IsDeprecated: false,
                    CreatedAt: DateTimeOffset.UtcNow,
                    UpdatedAt: DateTimeOffset.UtcNow,
                    AssignmentCount: 2),
            },
            TotalCount: 1,
            Page: 1,
            PageSize: 25);

        _definitionService
            .Setup(s => s.SearchAsync(
                It.Is<DefinitionQuery>(q => q.Search == "theme"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(definitions);

        // Act
        var json = await _sut.Search("theme");

        // Assert
        json.Should().Contain("app.theme.color");
        json.Should().Contain("Theme Color");

        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("items").GetArrayLength().Should().Be(1);

        _definitionService.Verify(
            s => s.SearchAsync(
                It.Is<DefinitionQuery>(q => q.Search == "theme"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetEffective_ReturnsResolvedValue()
    {
        // Arrange
        var resolved = new ResolvedSetting
        {
            Key = "app.theme",
            EffectiveValue = "\"dark\"",
            WinningScopeType = ScopeType.User,
            WinningScopeId = "user1",
            DataType = SettingDataType.String,
            IsSecret = false,
            IsDefault = false,
            IsValid = true,
        };

        _resolutionService
            .Setup(s => s.ResolveAsync("app.theme", It.IsAny<ResolutionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resolved);

        // Act
        var json = await _sut.GetEffective("app.theme", userId: "user1");

        // Assert
        json.Should().Contain("app.theme");
        json.Should().Contain("dark");
        json.Should().Contain("effectiveValue");

        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("key").GetString().Should().Be("app.theme");
        doc.RootElement.GetProperty("effectiveValue").GetString().Should().Be("\"dark\"");
        doc.RootElement.GetProperty("isDefault").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Explain_ReturnsSourceChain()
    {
        // Arrange
        var resolved = new ResolvedSetting
        {
            Key = "app.theme",
            EffectiveValue = "\"dark\"",
            WinningScopeType = ScopeType.User,
            WinningScopeId = "user1",
            DataType = SettingDataType.String,
            IsSecret = false,
            IsDefault = false,
            IsValid = true,
            SourceChain = new[]
            {
                new SourceChainEntry(ScopeType.Machine, null, "\"light\"", false),
                new SourceChainEntry(ScopeType.User, "user1", "\"dark\"", true),
            },
        };

        _resolutionService
            .Setup(s => s.ExplainAsync("app.theme", It.IsAny<ResolutionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resolved);

        // Act
        var json = await _sut.Explain("app.theme", userId: "user1");

        // Assert
        json.Should().Contain("sourceChain");
        json.Should().Contain("isWinner");

        var doc = JsonDocument.Parse(json);
        var chain = doc.RootElement.GetProperty("sourceChain");
        chain.GetArrayLength().Should().Be(2);

        // The winner entry should be marked
        var winnerEntry = chain.EnumerateArray().Single(e => e.GetProperty("isWinner").GetBoolean());
        winnerEntry.GetProperty("scopeType").GetString().Should().Be("User");
        winnerEntry.GetProperty("valueJson").GetString().Should().Be("\"dark\"");
    }

    [Fact]
    public async Task Categories_ReturnsDistinctCategories()
    {
        // Arrange
        var definitions = new PagedResult<DefinitionDto>(
            Items: new[]
            {
                new DefinitionDto(Guid.NewGuid(), "app.a", "testapp", "A", null, "UI",
                    SettingDataType.String, null, null, null, false, null, null, false,
                    DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 0),
                new DefinitionDto(Guid.NewGuid(), "app.b", "testapp", "B", null, "Security",
                    SettingDataType.String, null, null, null, false, null, null, false,
                    DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 0),
                new DefinitionDto(Guid.NewGuid(), "app.c", "testapp", "C", null, "UI",
                    SettingDataType.String, null, null, null, false, null, null, false,
                    DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 0),
                new DefinitionDto(Guid.NewGuid(), "app.d", "testapp", "D", null, null,
                    SettingDataType.String, null, null, null, false, null, null, false,
                    DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 0),
            },
            TotalCount: 4,
            Page: 1,
            PageSize: 1000);

        _definitionService
            .Setup(s => s.SearchAsync(It.IsAny<DefinitionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(definitions);

        // Act
        var json = await _sut.Categories();

        // Assert
        json.Should().Contain("categories");

        var doc = JsonDocument.Parse(json);
        var categories = doc.RootElement.GetProperty("categories");
        categories.GetArrayLength().Should().Be(2);

        var categoryList = categories.EnumerateArray().Select(e => e.GetString()).ToList();
        categoryList.Should().Contain("UI");
        categoryList.Should().Contain("Security");
        categoryList.Should().NotContain((string?)null);
    }

    [Fact]
    public async Task Audit_ReturnsAuditEvents()
    {
        // Arrange
        var events = new PagedResult<AuditEventDto>(
            Items: new[]
            {
                new AuditEventDto(
                    Id: Guid.NewGuid(),
                    EventType: AuditEventType.Updated,
                    DefinitionKey: "app.theme",
                    ScopeType: ScopeType.User,
                    ScopeId: "user1",
                    ActorType: "User",
                    ActorId: "admin@test.com",
                    BeforeJson: "\"light\"",
                    AfterJson: "\"dark\"",
                    CorrelationId: null,
                    CreatedAt: DateTimeOffset.UtcNow),
            },
            TotalCount: 1,
            Page: 1,
            PageSize: 25);

        _auditService
            .Setup(s => s.QueryAsync(It.IsAny<AuditQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(events);

        // Act
        var json = await _sut.Audit(definitionKey: "app.theme", limit: 10);

        // Assert
        json.Should().Contain("app.theme");
        json.Should().Contain("items");

        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("items").GetArrayLength().Should().Be(1);
        doc.RootElement.GetProperty("totalCount").GetInt32().Should().Be(1);

        var firstEvent = doc.RootElement.GetProperty("items")[0];
        firstEvent.GetProperty("definitionKey").GetString().Should().Be("app.theme");
        firstEvent.GetProperty("eventType").GetString().Should().Be("Updated");

        _auditService.Verify(
            s => s.QueryAsync(
                It.Is<AuditQuery>(q => q.DefinitionKey == "app.theme" && q.PageSize == 10),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
