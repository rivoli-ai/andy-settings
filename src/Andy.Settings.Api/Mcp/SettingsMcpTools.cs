using System.ComponentModel;
using System.Text.Json;
using Andy.Settings.Application.DTOs.Audit;
using Andy.Settings.Application.DTOs.Definitions;
using Andy.Settings.Application.DTOs.Effective;
using Andy.Settings.Application.DTOs.ImportExport;
using Andy.Settings.Application.DTOs.Secrets;
using Andy.Settings.Application.DTOs.Values;
using Andy.Settings.Application.Interfaces;
using Andy.Settings.Domain.Enums;
using Andy.Rbac.Authorization;
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
    private readonly ISecretService _secrets;
    private readonly ICurrentUserService _currentUser;

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
        IExportImportService exportImport,
        ISecretService secrets,
        ICurrentUserService currentUser)
    {
        _definitions = definitions;
        _resolution = resolution;
        _assignments = assignments;
        _audit = audit;
        _exportImport = exportImport;
        _secrets = secrets;
        _currentUser = currentUser;
    }

    // Identity of the caller behind this MCP tool invocation. The /mcp endpoint
    // requires authorization, so a token is present and its subject is the
    // actor. Every write used to hard-code a null actor, which meant
    // agent-driven changes — the ones most in need of attribution — were the
    // only ones recorded as coming from nobody (rivoli-ai/andy-settings#131).
    private string? ActorId => _currentUser.GetUserId();

    [McpServerTool(Name = "settings_list_definitions")]
    [RequirePermission("definition:read")]
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
    [RequirePermission("value:read")]
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
    [RequirePermission("value:write")]
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
        var result = await _assignments.SetAsync(dto, ActorId);
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    [McpServerTool(Name = "settings_delete_value")]
    [RequirePermission("value:delete")]
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
        await _assignments.DeleteAsync(assignment.Id, ActorId);
        return JsonSerializer.Serialize(new { success = true, message = $"Deleted assignment for '{definitionKey}' at scope {scopeType}/{scopeId ?? "(global)"}." }, JsonOptions);
    }

    [McpServerTool(Name = "settings_explain")]
    [RequirePermission("value:read")]
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
    [RequirePermission("definition:read")]
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
    [RequirePermission("audit:read")]
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
    [RequirePermission("definition:read")]
    [Description("List all distinct categories across setting definitions")]
    public async Task<string> Categories(string? applicationCode = null)
    {
        // DISTINCT in the database. This used to request a 1000-definition page
        // and reduce it client-side, which under-reported as soon as the
        // catalog exceeded that page — and silently, since a truncated page is
        // indistinguishable from a complete one here. The paging cap added in
        // #134 made the truncation start at 500 instead of 1000, so the
        // approximation had to go rather than be re-tuned.
        var categories = await _definitions.GetCategoriesAsync(applicationCode);
        return JsonSerializer.Serialize(new { categories }, JsonOptions);
    }

    [McpServerTool(Name = "settings_export")]
    [RequirePermission("export:read")]
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

    [McpServerTool(Name = "settings_get_definition")]
    [RequirePermission("definition:read")]
    [Description("Get a single setting definition by its key")]
    public async Task<string> GetDefinition(string key)
    {
        try
        {
            var result = await _definitions.GetAsync(key);
            if (result is null)
                return JsonSerializer.Serialize(new { error = $"Definition with key '{key}' not found." }, JsonOptions);

            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    [McpServerTool(Name = "settings_create_definition")]
    [RequirePermission("definition:write")]
    [Description("Create a new setting definition")]
    public async Task<string> CreateDefinition(
        string key,
        string applicationCode,
        string displayName,
        string dataType,
        string? description = null,
        string? category = null,
        string? defaultValueJson = null,
        bool isSecret = false)
    {
        try
        {
            if (!Enum.TryParse<SettingDataType>(dataType, ignoreCase: true, out var parsedDataType))
                return JsonSerializer.Serialize(new { error = $"Invalid dataType '{dataType}'. Valid values: {string.Join(", ", Enum.GetNames<SettingDataType>())}" }, JsonOptions);

            var dto = new CreateDefinitionDto
            {
                Key = key,
                ApplicationCode = applicationCode,
                DisplayName = displayName,
                DataType = parsedDataType,
                Description = description,
                Category = category,
                DefaultValueJson = defaultValueJson,
                IsSecret = isSecret,
            };
            var result = await _definitions.CreateAsync(dto, ActorId);
            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    [McpServerTool(Name = "settings_update_definition")]
    [RequirePermission("definition:write")]
    [Description("Update an existing setting definition by key")]
    public async Task<string> UpdateDefinition(
        string key,
        string? displayName = null,
        string? description = null,
        string? category = null,
        bool? isDeprecated = null)
    {
        try
        {
            var dto = new UpdateDefinitionDto
            {
                DisplayName = displayName,
                Description = description,
                Category = category,
                IsDeprecated = isDeprecated,
            };
            var result = await _definitions.UpdateAsync(key, dto, ActorId);
            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    [McpServerTool(Name = "settings_delete_definition")]
    [RequirePermission("definition:delete")]
    [Description("Delete a setting definition by key")]
    public async Task<string> DeleteDefinition(string key)
    {
        try
        {
            await _definitions.DeleteAsync(key, ActorId);
            return JsonSerializer.Serialize(new { success = true, message = $"Definition '{key}' deleted." }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    [McpServerTool(Name = "settings_list_values")]
    [RequirePermission("value:read")]
    [Description("List setting value assignments, optionally filtered by definition key and scope")]
    public async Task<string> ListValues(
        string? definitionKey = null,
        string? scopeType = null,
        string? scopeId = null,
        int page = 1,
        int pageSize = 25)
    {
        try
        {
            ScopeType? parsedScope = null;
            if (scopeType is not null)
            {
                if (!Enum.TryParse<ScopeType>(scopeType, ignoreCase: true, out var parsed))
                    return JsonSerializer.Serialize(new { error = $"Invalid scopeType '{scopeType}'. Valid values: {string.Join(", ", Enum.GetNames<ScopeType>())}" }, JsonOptions);
                parsedScope = parsed;
            }

            var result = await _assignments.ListByScopeAsync(definitionKey, parsedScope, scopeId, page, pageSize);
            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    [McpServerTool(Name = "settings_set_secret")]
    [RequirePermission("secret:write")]
    [Description("Set an encrypted secret value for a setting definition at a specific scope")]
    public async Task<string> SetSecret(
        string definitionKey,
        string scopeType,
        string? scopeId = null,
        string value = "")
    {
        try
        {
            if (!Enum.TryParse<ScopeType>(scopeType, ignoreCase: true, out var parsedScope))
                return JsonSerializer.Serialize(new { error = $"Invalid scopeType '{scopeType}'. Valid values: {string.Join(", ", Enum.GetNames<ScopeType>())}" }, JsonOptions);

            var dto = new SetSecretDto
            {
                DefinitionKey = definitionKey,
                ScopeType = parsedScope,
                ScopeId = scopeId,
                PlaintextValue = value,
            };
            var result = await _secrets.SetSecretAsync(dto, ActorId);
            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    [McpServerTool(Name = "settings_get_secret")]
    [RequirePermission("secret:read")]
    [Description("Get a decrypted secret value for a setting definition (requires permission)")]
    public async Task<string> GetSecret(
        string definitionKey,
        string? scopeType = null,
        string? scopeId = null)
    {
        try
        {
            ScopeType parsedScope = ScopeType.Machine;
            if (scopeType is not null)
            {
                if (!Enum.TryParse<ScopeType>(scopeType, ignoreCase: true, out parsedScope))
                    return JsonSerializer.Serialize(new { error = $"Invalid scopeType '{scopeType}'. Valid values: {string.Join(", ", Enum.GetNames<ScopeType>())}" }, JsonOptions);
            }

            var dto = new GetSecretDto
            {
                DefinitionKey = definitionKey,
                ScopeType = parsedScope,
                ScopeId = scopeId,
            };
            var result = await _secrets.GetSecretAsync(dto, ActorId);
            if (result is null)
                return JsonSerializer.Serialize(new { error = $"No secret found for definition '{definitionKey}' at scope {scopeType ?? "Machine"}/{scopeId ?? "(none)"}." }, JsonOptions);

            return JsonSerializer.Serialize(new { definitionKey, scopeType = parsedScope.ToString(), scopeId, value = result }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    [McpServerTool(Name = "settings_rotate_secret")]
    [RequirePermission("secret:write")]
    [Description("Rotate an encrypted secret value for a setting definition")]
    public async Task<string> RotateSecret(
        string definitionKey,
        string scopeType,
        string? scopeId = null,
        string newValue = "")
    {
        try
        {
            if (!Enum.TryParse<ScopeType>(scopeType, ignoreCase: true, out var parsedScope))
                return JsonSerializer.Serialize(new { error = $"Invalid scopeType '{scopeType}'. Valid values: {string.Join(", ", Enum.GetNames<ScopeType>())}" }, JsonOptions);

            var dto = new RotateSecretDto
            {
                DefinitionKey = definitionKey,
                ScopeType = parsedScope,
                ScopeId = scopeId,
                NewPlaintextValue = newValue,
            };
            var result = await _secrets.RotateSecretAsync(dto, ActorId);
            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    [McpServerTool(Name = "settings_delete_secret")]
    [RequirePermission("secret:write")]
    [Description("Delete a secret at a specific scope, or every scope for the definition when allScopes is true")]
    public async Task<string> DeleteSecret(
        string definitionKey,
        string? scopeType = null,
        string? scopeId = null,
        bool allScopes = false)
    {
        try
        {
            // An agent must state which scope it means. Defaulting to
            // all-scopes would let a model clear one user's credential and
            // silently wipe every other user's along with it
            // (rivoli-ai/andy-settings#138).
            if (scopeType is null && !allScopes)
            {
                return JsonSerializer.Serialize(new
                {
                    error = "Specify scopeType (plus scopeId for non-Machine scopes), "
                          + "or pass allScopes=true to delete every stored secret for this definition."
                }, JsonOptions);
            }

            ScopeType? parsedScope = null;
            if (scopeType is not null)
            {
                if (!Enum.TryParse<ScopeType>(scopeType, ignoreCase: true, out var parsed))
                    return JsonSerializer.Serialize(new { error = $"Invalid scopeType '{scopeType}'. Valid values: {string.Join(", ", Enum.GetNames<ScopeType>())}" }, JsonOptions);
                parsedScope = parsed;
            }

            var deleted = await _secrets.DeleteSecretAsync(definitionKey, parsedScope, scopeId, ActorId);

            return deleted == 0
                ? JsonSerializer.Serialize(new { error = $"No stored secret found for definition '{definitionKey}' at the requested scope." }, JsonOptions)
                : JsonSerializer.Serialize(new { success = true, deleted, message = $"Deleted {deleted} secret(s) for definition '{definitionKey}'." }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    [McpServerTool(Name = "settings_resolve_batch")]
    [RequirePermission("value:read")]
    [Description("Resolve multiple setting keys at once, returning effective values for the given context")]
    public async Task<string> ResolveBatch(
        string keys,
        string? applicationCode = null,
        string? userId = null,
        string? teamId = null)
    {
        try
        {
            var keyList = keys.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var context = new ResolutionContext
            {
                ApplicationCode = applicationCode,
                UserId = userId,
                TeamId = teamId,
            };
            var result = await _resolution.ResolveBatchAsync(keyList, context);
            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    [McpServerTool(Name = "settings_import")]
    [RequirePermission("import:write")]
    [Description("Import settings from JSON data")]
    public async Task<string> Import(string jsonData)
    {
        try
        {
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(jsonData));
            var options = new ImportOptions();
            var result = await _exportImport.ImportAsync(stream, options, ActorId);
            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    [McpServerTool(Name = "settings_import_preview")]
    [RequirePermission("import:write")]
    [Description("Preview what an import would change without applying it")]
    public async Task<string> ImportPreview(string jsonData)
    {
        try
        {
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(jsonData));
            var result = await _exportImport.PreviewImportAsync(stream);
            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }
}
