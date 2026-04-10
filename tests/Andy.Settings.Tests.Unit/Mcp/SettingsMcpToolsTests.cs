using System.Text.Json;
using Andy.Settings.Api.Mcp;
using Andy.Settings.Application.DTOs.Audit;
using Andy.Settings.Application.DTOs.Common;
using Andy.Settings.Application.DTOs.Definitions;
using Andy.Settings.Application.DTOs.Effective;
using Andy.Settings.Application.DTOs.ImportExport;
using Andy.Settings.Application.DTOs.Secrets;
using Andy.Settings.Application.DTOs.Values;
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

    [Fact]
    public async Task SetValue_ReturnsAssignmentJson()
    {
        // Arrange
        var assignment = new AssignmentDto(
            Id: Guid.NewGuid(),
            DefinitionId: Guid.NewGuid(),
            DefinitionKey: "app.theme",
            ScopeType: ScopeType.User,
            ScopeId: "user1",
            ValueJson: "\"dark\"",
            Etag: "abc123",
            Version: 1,
            UpdatedBy: null,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow);

        _assignmentService
            .Setup(s => s.SetAsync(It.IsAny<SetValueDto>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(assignment);

        // Act
        var json = await _sut.SetValue("app.theme", "User", "user1", "\"dark\"");

        // Assert
        json.Should().Contain("app.theme");
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("definitionKey").GetString().Should().Be("app.theme");
        doc.RootElement.GetProperty("valueJson").GetString().Should().Be("\"dark\"");
    }

    [Fact]
    public async Task DeleteValue_WhenAssignmentExists_DeletesAndReturnsSuccess()
    {
        // Arrange
        var assignmentId = Guid.NewGuid();
        var assignments = new PagedResult<AssignmentDto>(
            Items: new[]
            {
                new AssignmentDto(
                    Id: assignmentId,
                    DefinitionId: Guid.NewGuid(),
                    DefinitionKey: "app.theme",
                    ScopeType: ScopeType.User,
                    ScopeId: "user1",
                    ValueJson: "\"dark\"",
                    Etag: "abc",
                    Version: 1,
                    UpdatedBy: null,
                    CreatedAt: DateTimeOffset.UtcNow,
                    UpdatedAt: DateTimeOffset.UtcNow),
            },
            TotalCount: 1,
            Page: 1,
            PageSize: 1);

        _assignmentService
            .Setup(s => s.ListByScopeAsync("app.theme", ScopeType.User, "user1", 1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assignments);

        _assignmentService
            .Setup(s => s.DeleteAsync(assignmentId, null, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var json = await _sut.DeleteValue("app.theme", "User", "user1");

        // Assert
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        _assignmentService.Verify(s => s.DeleteAsync(assignmentId, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetDefinition_ReturnsDefinitionJson()
    {
        // Arrange
        var definition = new DefinitionDto(
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
            AssignmentCount: 0);

        _definitionService
            .Setup(s => s.GetAsync("app.theme", It.IsAny<CancellationToken>()))
            .ReturnsAsync(definition);

        // Act
        var json = await _sut.GetDefinition("app.theme");

        // Assert
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("key").GetString().Should().Be("app.theme");
        doc.RootElement.GetProperty("displayName").GetString().Should().Be("Theme");
    }

    [Fact]
    public async Task CreateDefinition_ReturnsCreatedDefinition()
    {
        // Arrange
        var definition = new DefinitionDto(
            Id: Guid.NewGuid(),
            Key: "app.new",
            ApplicationCode: "testapp",
            DisplayName: "New Setting",
            Description: "A new setting",
            Category: "General",
            DataType: SettingDataType.String,
            DefaultValueJson: null,
            ValidationJson: null,
            UiSchemaJson: null,
            IsSecret: false,
            AllowedScopesJson: null,
            TagsJson: null,
            IsDeprecated: false,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow,
            AssignmentCount: 0);

        _definitionService
            .Setup(s => s.CreateAsync(It.IsAny<CreateDefinitionDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(definition);

        // Act
        var json = await _sut.CreateDefinition("app.new", "testapp", "New Setting", "String", "A new setting", "General");

        // Assert
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("key").GetString().Should().Be("app.new");
        doc.RootElement.GetProperty("displayName").GetString().Should().Be("New Setting");
    }

    [Fact]
    public async Task UpdateDefinition_ReturnsUpdatedDefinition()
    {
        // Arrange
        var definition = new DefinitionDto(
            Id: Guid.NewGuid(),
            Key: "app.theme",
            ApplicationCode: "testapp",
            DisplayName: "Updated Theme",
            Description: "Updated desc",
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
            AssignmentCount: 0);

        _definitionService
            .Setup(s => s.UpdateAsync("app.theme", It.IsAny<UpdateDefinitionDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(definition);

        // Act
        var json = await _sut.UpdateDefinition("app.theme", displayName: "Updated Theme", description: "Updated desc");

        // Assert
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("key").GetString().Should().Be("app.theme");
        doc.RootElement.GetProperty("displayName").GetString().Should().Be("Updated Theme");
    }

    [Fact]
    public async Task DeleteDefinition_ReturnsSuccessMessage()
    {
        // Arrange
        _definitionService
            .Setup(s => s.DeleteAsync("app.theme", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var json = await _sut.DeleteDefinition("app.theme");

        // Assert
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("message").GetString().Should().Contain("app.theme");
        _definitionService.Verify(s => s.DeleteAsync("app.theme", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListValues_ReturnsPagedAssignments()
    {
        // Arrange
        var assignments = new PagedResult<AssignmentDto>(
            Items: new[]
            {
                new AssignmentDto(
                    Id: Guid.NewGuid(),
                    DefinitionId: Guid.NewGuid(),
                    DefinitionKey: "app.theme",
                    ScopeType: ScopeType.User,
                    ScopeId: "user1",
                    ValueJson: "\"dark\"",
                    Etag: "abc",
                    Version: 1,
                    UpdatedBy: null,
                    CreatedAt: DateTimeOffset.UtcNow,
                    UpdatedAt: DateTimeOffset.UtcNow),
            },
            TotalCount: 1,
            Page: 1,
            PageSize: 25);

        _assignmentService
            .Setup(s => s.ListByScopeAsync("app.theme", null, null, 1, 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assignments);

        // Act
        var json = await _sut.ListValues(definitionKey: "app.theme");

        // Assert
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("items").GetArrayLength().Should().Be(1);
        doc.RootElement.GetProperty("totalCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task SetSecret_ReturnsSecretMetadata()
    {
        // Arrange
        var metadata = new SecretMetadataDto(
            Id: Guid.NewGuid(),
            DefinitionId: Guid.NewGuid(),
            DefinitionKey: "app.api-key",
            ScopeType: ScopeType.Machine,
            ScopeId: null,
            UpdatedBy: null,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow);

        _secretService
            .Setup(s => s.SetSecretAsync(It.IsAny<SetSecretDto>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(metadata);

        // Act
        var json = await _sut.SetSecret("app.api-key", "Machine", value: "secret-value");

        // Assert
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("definitionKey").GetString().Should().Be("app.api-key");
    }

    [Fact]
    public async Task GetSecret_ReturnsDecryptedValue()
    {
        // Arrange
        _secretService
            .Setup(s => s.GetSecretAsync(It.IsAny<GetSecretDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("my-secret-value");

        // Act
        var json = await _sut.GetSecret("app.api-key", "Machine");

        // Assert
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("definitionKey").GetString().Should().Be("app.api-key");
        doc.RootElement.GetProperty("value").GetString().Should().Be("my-secret-value");
    }

    [Fact]
    public async Task Export_ReturnsExportResult()
    {
        // Arrange
        var exportResult = new ExportResult
        {
            Format = "json",
            ExportedAt = DateTimeOffset.UtcNow,
            DefinitionCount = 5,
            AssignmentCount = 10,
            Data = "{\"definitions\":[]}",
        };

        _exportImportService
            .Setup(s => s.ExportAsync(It.IsAny<ExportOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(exportResult);

        // Act
        var json = await _sut.Export("testapp");

        // Assert
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("format").GetString().Should().Be("json");
        doc.RootElement.GetProperty("definitionCount").GetInt32().Should().Be(5);
        doc.RootElement.GetProperty("assignmentCount").GetInt32().Should().Be(10);
    }

    [Fact]
    public async Task RotateSecret_ReturnsSecretMetadata()
    {
        // Arrange
        var metadata = new SecretMetadataDto(
            Id: Guid.NewGuid(),
            DefinitionId: Guid.NewGuid(),
            DefinitionKey: "app.api-key",
            ScopeType: ScopeType.Machine,
            ScopeId: null,
            UpdatedBy: null,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow);

        _secretService
            .Setup(s => s.RotateSecretAsync(It.IsAny<RotateSecretDto>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(metadata);

        // Act
        var json = await _sut.RotateSecret("app.api-key", "Machine", newValue: "new-secret-value");

        // Assert
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("definitionKey").GetString().Should().Be("app.api-key");
    }

    [Fact]
    public async Task DeleteSecret_ReturnsSuccessMessage()
    {
        // Arrange
        _secretService
            .Setup(s => s.DeleteSecretAsync("app.api-key", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var json = await _sut.DeleteSecret("app.api-key");

        // Assert
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("message").GetString().Should().Contain("app.api-key");
        _secretService.Verify(s => s.DeleteSecretAsync("app.api-key", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveBatch_ReturnsResolvedSettings()
    {
        // Arrange
        var resolved = new List<ResolvedSetting>
        {
            new()
            {
                Key = "app.theme",
                EffectiveValue = "\"dark\"",
                WinningScopeType = ScopeType.User,
                WinningScopeId = "user1",
                DataType = SettingDataType.String,
                IsSecret = false,
                IsDefault = false,
                IsValid = true,
            },
            new()
            {
                Key = "app.language",
                EffectiveValue = "\"en\"",
                WinningScopeType = ScopeType.Machine,
                WinningScopeId = null,
                DataType = SettingDataType.String,
                IsSecret = false,
                IsDefault = true,
                IsValid = true,
            },
        };

        _resolutionService
            .Setup(s => s.ResolveBatchAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<ResolutionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resolved);

        // Act
        var json = await _sut.ResolveBatch("app.theme, app.language", userId: "user1");

        // Assert
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task Import_ReturnsImportResult()
    {
        // Arrange
        var importResult = new ImportResult
        {
            DefinitionsCreated = 2,
            DefinitionsUpdated = 1,
            AssignmentsCreated = 3,
            AssignmentsUpdated = 0,
            Warnings = [],
        };

        _exportImportService
            .Setup(s => s.ImportAsync(It.IsAny<Stream>(), It.IsAny<ImportOptions>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(importResult);

        // Act
        var json = await _sut.Import("{\"definitions\":[]}");

        // Assert
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("definitionsCreated").GetInt32().Should().Be(2);
        doc.RootElement.GetProperty("definitionsUpdated").GetInt32().Should().Be(1);
        doc.RootElement.GetProperty("assignmentsCreated").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task ImportPreview_ReturnsPreviewResult()
    {
        // Arrange
        var preview = new ImportPreview
        {
            Additions = new[] { new ImportChange("app.new", "Add", null, "\"value\"") },
            Modifications = new[] { new ImportChange("app.existing", "Modify", "\"old\"", "\"new\"") },
            Deletions = [],
            ValidationErrors = [],
        };

        _exportImportService
            .Setup(s => s.PreviewImportAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(preview);

        // Act
        var json = await _sut.ImportPreview("{\"definitions\":[]}");

        // Assert
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("additions").GetArrayLength().Should().Be(1);
        doc.RootElement.GetProperty("modifications").GetArrayLength().Should().Be(1);
        doc.RootElement.GetProperty("isValid").GetBoolean().Should().BeTrue();
    }
}
