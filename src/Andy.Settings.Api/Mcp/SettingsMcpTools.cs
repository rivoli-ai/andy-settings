using System.ComponentModel;
using System.Text.Json;
using Andy.Settings.Application.DTOs.Audit;
using Andy.Settings.Application.DTOs.Definitions;
using Andy.Settings.Application.DTOs.Effective;
using Andy.Settings.Application.DTOs.ImportExport;
using Andy.Settings.Application.DTOs.Values;
using Andy.Settings.Application.Interfaces;
using Andy.Settings.Domain.Enums;
using ModelContextProtocol.Server;

namespace Andy.Settings.Api.Mcp;

[McpServerToolType]
public class SettingsMcpTools
{
    private readonly IDefinitionService _definitions;
    private readonly IResolutionService _resolution;
    private readonly IAssignmentService _assignments;
    private readonly IAuditService _audit;
    private readonly IExportImportService _exportImport;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public SettingsMcpTools(
        IDefinitionService definitions,
        IResolutionService resolution,
        IAssignmentService assignments,
        IAuditService audit,
        IExportImportService exportImport)
    {
        _definitions = definitions;
        _resolution = resolution;
        _assignments = assignments;
        _audit = audit;
        _exportImport = exportImport;
    }

    [McpServerTool(Name = "settings_list_definitions")]
    [Description("List setting definitions, optionally filtered by application code or category")]
    public async Task<string> ListDefinitions(
        string? applicationCode = null,
        string? category = null,
        int page = 1,
        int pageSize = 25)
    {
        var query = new DefinitionQuery
        {
            ApplicationCode = applicationCode,
            Category = category,
            Page = page,
            PageSize = pageSize,
        };
        var result = await _definitions.SearchAsync(query);
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    [McpServerTool(Name = "settings_get_effective")]
    [Description("Resolve the effective value of a setting for a given context")]
    public async Task<string> GetEffective(
        string key,
        string? applicationCode = null,
        string? userId = null,
        string? teamId = null,
        string? workspaceId = null)
    {
        var context = new ResolutionContext
        {
            ApplicationCode = applicationCode,
            UserId = userId,
            TeamId = teamId,
            WorkspaceId = workspaceId,
        };
        var result = await _resolution.ResolveAsync(key, context);
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    [McpServerTool(Name = "settings_set_value")]
    [Description("Set a setting value at a specific scope")]
    public async Task<string> SetValue(
        string definitionKey,
        string scopeType,
        string? scopeId = null,
        string valueJson = "")
    {
        if (!Enum.TryParse<ScopeType>(scopeType, ignoreCase: true, out var parsedScope))
            return JsonSerializer.Serialize(new { error = $"Invalid scopeType '{scopeType}'. Valid values: {string.Join(", ", Enum.GetNames<ScopeType>())}" }, JsonOptions);

        var dto = new SetValueDto
        {
            DefinitionKey = definitionKey,
            ScopeType = parsedScope,
            ScopeId = scopeId,
            ValueJson = valueJson,
        };
        var result = await _assignments.SetAsync(dto, actorId: null);
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    [McpServerTool(Name = "settings_delete_value")]
    [Description("Delete a scoped setting value")]
    public async Task<string> DeleteValue(
        string definitionKey,
        string scopeType,
        string? scopeId = null)
    {
        if (!Enum.TryParse<ScopeType>(scopeType, ignoreCase: true, out var parsedScope))
            return JsonSerializer.Serialize(new { error = $"Invalid scopeType '{scopeType}'. Valid values: {string.Join(", ", Enum.GetNames<ScopeType>())}" }, JsonOptions);

        // Find the assignment by definition key + scope to get its ID
        var assignments = await _assignments.ListByScopeAsync(definitionKey, parsedScope, scopeId, 1, 1);
        if (assignments.TotalCount == 0)
            return JsonSerializer.Serialize(new { error = "No assignment found matching the specified definition key, scope type, and scope ID." }, JsonOptions);

        var assignment = assignments.Items[0];
        await _assignments.DeleteAsync(assignment.Id, actorId: null);
        return JsonSerializer.Serialize(new { success = true, message = $"Deleted assignment for '{definitionKey}' at scope {scopeType}/{scopeId ?? "(global)"}." }, JsonOptions);
    }

    [McpServerTool(Name = "settings_explain")]
    [Description("Explain why a setting has its current effective value, showing the full scope resolution chain")]
    public async Task<string> Explain(
        string key,
        string? applicationCode = null,
        string? userId = null,
        string? teamId = null)
    {
        var context = new ResolutionContext
        {
            ApplicationCode = applicationCode,
            UserId = userId,
            TeamId = teamId,
        };
        var result = await _resolution.ExplainAsync(key, context);
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    [McpServerTool(Name = "settings_search")]
    [Description("Search setting definitions by keyword")]
    public async Task<string> Search(
        string query,
        string? applicationCode = null)
    {
        var searchQuery = new DefinitionQuery
        {
            Search = query,
            ApplicationCode = applicationCode,
        };
        var result = await _definitions.SearchAsync(searchQuery);
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    [McpServerTool(Name = "settings_audit")]
    [Description("Get recent audit events for setting changes")]
    public async Task<string> Audit(
        string? definitionKey = null,
        int limit = 25)
    {
        var query = new AuditQuery
        {
            DefinitionKey = definitionKey,
            PageSize = limit,
        };
        var result = await _audit.QueryAsync(query);
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    [McpServerTool(Name = "settings_categories")]
    [Description("List all distinct categories across setting definitions")]
    public async Task<string> Categories()
    {
        // Fetch a large page of definitions and extract distinct categories
        var query = new DefinitionQuery { PageSize = 1000 };
        var result = await _definitions.SearchAsync(query);
        var categories = result.Items
            .Select(d => d.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .OrderBy(c => c)
            .ToList();
        return JsonSerializer.Serialize(new { categories }, JsonOptions);
    }

    [McpServerTool(Name = "settings_export")]
    [Description("Export settings as JSON")]
    public async Task<string> Export(
        string? applicationCode = null)
    {
        var options = new ExportOptions
        {
            ApplicationCode = applicationCode,
            Format = "json",
        };
        var result = await _exportImport.ExportAsync(options);
        return JsonSerializer.Serialize(result, JsonOptions);
    }
}
