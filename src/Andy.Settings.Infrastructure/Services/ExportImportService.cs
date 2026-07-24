using System.Text.Json;
using System.Text.Json.Serialization;
using Andy.Settings.Application.DTOs.ImportExport;
using Andy.Settings.Application.DTOs.Audit;
using Andy.Settings.Application.Interfaces;
using Andy.Settings.Application.Messaging.Events;
using Andy.Settings.Domain.Entities;
using Andy.Settings.Domain.Enums;
using Andy.Settings.Infrastructure.Data;
using Andy.Settings.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Andy.Settings.Infrastructure.Services;

public class ExportImportService : IExportImportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly SettingsDbContext _db;
    private readonly IValidationService _validation;
    private readonly IAuditService _audit;

    public ExportImportService(SettingsDbContext db, IValidationService validation, IAuditService audit)
    {
        _db = db;
        _validation = validation;
        _audit = audit;
    }

    public async Task<ExportResult> ExportAsync(ExportOptions options, CancellationToken ct = default)
    {
        var definitionsQuery = _db.SettingDefinitions.AsNoTracking().AsQueryable();
        if (!string.IsNullOrEmpty(options.ApplicationCode))
            definitionsQuery = definitionsQuery.Where(d => d.ApplicationCode == options.ApplicationCode);

        var definitions = await definitionsQuery.OrderBy(d => d.Key).ToListAsync(ct);
        var ordinaryDefinitions = definitions.Where(d => !d.IsSecret && d.DataType != SettingDataType.Secret).ToList();
        var ordinaryIds = ordinaryDefinitions.Select(d => d.Id).ToHashSet();

        var assignments = await _db.SettingAssignments.AsNoTracking()
            .Where(a => ordinaryIds.Contains(a.DefinitionId))
            .OrderBy(a => a.DefinitionId).ThenBy(a => a.ScopeType).ThenBy(a => a.ScopeId)
            .ToListAsync(ct);
        var keysById = ordinaryDefinitions.ToDictionary(d => d.Id, d => d.Key);

        var exportData = new ImportDocument
        {
            Definitions = definitions.Select(d => new ImportDefinition
            {
                Key = d.Key,
                ApplicationCode = d.ApplicationCode,
                DisplayName = d.DisplayName,
                Description = d.Description,
                Category = d.Category,
                DataType = d.DataType,
                DefaultValueJson = d.IsSecret ? null : d.DefaultValueJson,
                ValidationJson = d.ValidationJson,
                UiSchemaJson = d.UiSchemaJson,
                IsSecret = d.IsSecret,
                AllowedScopesJson = d.AllowedScopesJson,
                TagsJson = d.TagsJson,
                IsDeprecated = d.IsDeprecated
            }).ToList(),
            Assignments = assignments.Select(a => new ImportAssignment
            {
                DefinitionKey = keysById[a.DefinitionId],
                ScopeType = a.ScopeType,
                ScopeId = a.ScopeId,
                ValueJson = a.ValueJson
            }).ToList()
        };

        return new ExportResult
        {
            Format = options.Format ?? "json",
            ExportedAt = DateTimeOffset.UtcNow,
            DefinitionCount = definitions.Count,
            AssignmentCount = assignments.Count,
            Data = JsonSerializer.Serialize(exportData, JsonOptions)
        };
    }

    public async Task<ImportPreview> PreviewImportAsync(Stream data, CancellationToken ct = default)
    {
        var (document, errors) = await ReadAndValidateAsync(data, ct);
        if (document is null)
            return new ImportPreview { ValidationErrors = errors };

        var keys = document.Definitions.Select(d => d.Key).ToHashSet(StringComparer.Ordinal);
        var existingDefinitions = await _db.SettingDefinitions.AsNoTracking()
            .Where(d => keys.Contains(d.Key)).ToDictionaryAsync(d => d.Key, ct);

        var additions = new List<ImportChange>();
        var modifications = new List<ImportChange>();
        foreach (var definition in document.Definitions)
        {
            if (!existingDefinitions.TryGetValue(definition.Key, out var existing))
                additions.Add(new(definition.Key, "DefinitionCreated", null, definition.DisplayName));
            else if (!DefinitionMatches(existing, definition))
                modifications.Add(new(definition.Key, "DefinitionUpdated", existing.DisplayName, definition.DisplayName));
        }

        var definitionIds = existingDefinitions.Values.Select(d => d.Id).ToList();
        var existingAssignments = await _db.SettingAssignments.AsNoTracking()
            .Where(a => definitionIds.Contains(a.DefinitionId)).ToListAsync(ct);

        foreach (var assignment in document.Assignments)
        {
            var definition = existingDefinitions.GetValueOrDefault(assignment.DefinitionKey);
            var existing = definition is null ? null : existingAssignments.FirstOrDefault(a =>
                a.DefinitionId == definition.Id && a.ScopeType == assignment.ScopeType && a.ScopeId == assignment.ScopeId);
            var change = new ImportChange(assignment.DefinitionKey,
                existing is null ? "AssignmentCreated" : "AssignmentUpdated",
                existing?.ValueJson, assignment.ValueJson);
            if (existing is null) additions.Add(change);
            else if (existing.ValueJson != assignment.ValueJson) modifications.Add(change);
        }

        return new ImportPreview
        {
            Additions = additions,
            Modifications = modifications,
            ValidationErrors = errors
        };
    }

    public async Task<ImportResult> ImportAsync(
        Stream data, ImportOptions options, string? actorId, CancellationToken ct = default)
    {
        var (document, errors) = await ReadAndValidateAsync(data, ct);
        if (document is null || errors.Count > 0)
            throw new InvalidDataException(string.Join(" ", errors));

        if (options.DryRun)
            return new ImportResult { Warnings = ["Dry run completed; no changes were applied."] };

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        var createdDefinitions = 0;
        var updatedDefinitions = 0;
        var createdAssignments = 0;
        var updatedAssignments = 0;
        var now = DateTimeOffset.UtcNow;

        foreach (var item in document.Definitions)
        {
            var definition = await _db.SettingDefinitions.FirstOrDefaultAsync(d => d.Key == item.Key, ct);
            if (definition is null)
            {
                definition = new SettingDefinition { Id = Guid.NewGuid(), Key = item.Key, CreatedAt = now };
                ApplyDefinition(definition, item, now);
                _db.SettingDefinitions.Add(definition);
                createdDefinitions++;
            }
            else if (!DefinitionMatches(definition, item))
            {
                var wasSecret = definition.IsSecret || definition.DataType == SettingDataType.Secret;
                var willBeSecret = item.IsSecret || item.DataType == SettingDataType.Secret;
                if (wasSecret != willBeSecret &&
                    (await _db.SettingAssignments.AnyAsync(a => a.DefinitionId == definition.Id, ct) ||
                     await _db.EncryptedSecrets.AnyAsync(s => s.DefinitionId == definition.Id, ct)))
                    throw new InvalidDataException(
                        $"Definition '{item.Key}' has stored values and cannot switch secret storage mode.");
                ApplyDefinition(definition, item, now);
                updatedDefinitions++;
            }
        }

        await _db.SaveChangesAsync(ct);
        var importedDefinitions = await _db.SettingDefinitions
            .Where(d => document.Definitions.Select(x => x.Key).Contains(d.Key))
            .ToDictionaryAsync(d => d.Key, ct);

        // Changes are collected as they are applied so the audit rows can be
        // written after the transaction commits, while the config events go
        // into the same SaveChanges as the mutation itself.
        var auditable = new List<(string Key, ScopeType ScopeType, string? ScopeId, string? Before, string After)>();

        foreach (var item in document.Assignments)
        {
            var definition = importedDefinitions[item.DefinitionKey];
            var existing = await _db.SettingAssignments.FirstOrDefaultAsync(a =>
                a.DefinitionId == definition.Id && a.ScopeType == item.ScopeType &&
                a.ScopeKey == (item.ScopeId ?? string.Empty), ct);
            if (existing is null)
            {
                var created = new SettingAssignment
                {
                    Id = Guid.NewGuid(), DefinitionId = definition.Id, ScopeType = item.ScopeType,
                    ScopeId = item.ScopeId, ValueJson = item.ValueJson, Etag = Guid.NewGuid().ToString("N"),
                    Version = 1, UpdatedBy = actorId, CreatedAt = now, UpdatedAt = now
                };
                _db.SettingAssignments.Add(created);

                // Import wrote assignments directly and published nothing, so
                // every consumer kept serving the pre-import value indefinitely
                // (rivoli-ai/andy-settings#135). Same outbox helper the REST
                // write path uses, in the same unit of work.
                _db.AppendConfigChanged(created, definition, ConfigEventKind.Created, item.ValueJson);
                auditable.Add((definition.Key, item.ScopeType, item.ScopeId, null, item.ValueJson));
                createdAssignments++;
            }
            else if (existing.ValueJson != item.ValueJson)
            {
                var before = existing.ValueJson;
                existing.ValueJson = item.ValueJson;
                existing.Etag = Guid.NewGuid().ToString("N");
                existing.Version++;
                existing.UpdatedBy = actorId;
                existing.UpdatedAt = now;

                _db.AppendConfigChanged(existing, definition, ConfigEventKind.Updated, item.ValueJson);
                auditable.Add((definition.Key, item.ScopeType, item.ScopeId, before, item.ValueJson));
                updatedAssignments++;
            }
        }

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        // AuditEventType.Imported was declared but never written by anything.
        // One row per changed key, so "what did that import actually touch?"
        // is answerable from the audit trail.
        foreach (var (key, scopeType, scopeId, before, after) in auditable)
        {
            await _audit.RecordAsync(new(Guid.NewGuid(), AuditEventType.Imported,
                key, scopeType, scopeId, "User", actorId,
                before, after, null, DateTimeOffset.UtcNow), ct);
        }
        return new ImportResult
        {
            DefinitionsCreated = createdDefinitions,
            DefinitionsUpdated = updatedDefinitions,
            AssignmentsCreated = createdAssignments,
            AssignmentsUpdated = updatedAssignments
        };
    }

    private async Task<(ImportDocument? Document, IReadOnlyList<string> Errors)> ReadAndValidateAsync(
        Stream data, CancellationToken ct)
    {
        ImportDocument? document;
        try
        {
            document = await JsonSerializer.DeserializeAsync<ImportDocument>(data, JsonOptions, ct);
        }
        catch (JsonException ex)
        {
            return (null, [$"Invalid import JSON: {ex.Message}"]);
        }

        if (document is null)
            return (null, ["Import document is empty."]);

        var errors = new List<string>();
        var duplicateKeys = document.Definitions.GroupBy(d => d.Key, StringComparer.Ordinal)
            .Where(g => g.Count() > 1).Select(g => g.Key);
        errors.AddRange(duplicateKeys.Select(key => $"Definition '{key}' appears more than once."));
        var definitions = document.Definitions.GroupBy(d => d.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        foreach (var definition in document.Definitions)
        {
            if (string.IsNullOrWhiteSpace(definition.Key) || string.IsNullOrWhiteSpace(definition.ApplicationCode) ||
                string.IsNullOrWhiteSpace(definition.DisplayName))
                errors.Add("Every definition requires key, applicationCode, and displayName.");
            if ((definition.IsSecret || definition.DataType == SettingDataType.Secret) && definition.DefaultValueJson is not null)
                errors.Add($"Secret definition '{definition.Key}' cannot contain a plaintext default value.");
        }

        var assignmentKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var assignment in document.Assignments)
        {
            if (!definitions.TryGetValue(assignment.DefinitionKey, out var definition))
            {
                errors.Add($"Assignment references missing definition '{assignment.DefinitionKey}'.");
                continue;
            }
            if (definition.IsSecret || definition.DataType == SettingDataType.Secret)
                errors.Add($"Secret definition '{definition.Key}' cannot be imported through plaintext assignments.");
            var identity = $"{assignment.DefinitionKey}\u001f{assignment.ScopeType}\u001f{assignment.ScopeId}";
            if (!assignmentKeys.Add(identity))
                errors.Add($"Duplicate assignment for '{assignment.DefinitionKey}' at {assignment.ScopeType}/{assignment.ScopeId ?? "(global)"}.");
            if (assignment.ScopeType == ScopeType.Machine && assignment.ScopeId is not null)
                errors.Add($"Machine assignment '{assignment.DefinitionKey}' must not specify scopeId.");
            if (assignment.ScopeType != ScopeType.Machine && string.IsNullOrWhiteSpace(assignment.ScopeId))
                errors.Add($"{assignment.ScopeType} assignment '{assignment.DefinitionKey}' requires scopeId.");
            if (!IsAllowedScope(definition.AllowedScopesJson, assignment.ScopeType, out var scopeError))
                errors.Add($"Assignment '{assignment.DefinitionKey}' is invalid: {scopeError}");
            var validationError = _validation.ValidateValue(ToEntity(definition), assignment.ValueJson);
            if (validationError is not null)
                errors.Add($"Assignment '{assignment.DefinitionKey}' is invalid: {validationError}");
        }

        return (document, errors);
    }

    private static SettingDefinition ToEntity(ImportDefinition item) => new()
    {
        Key = item.Key, ApplicationCode = item.ApplicationCode, DisplayName = item.DisplayName,
        DataType = item.DataType, ValidationJson = item.ValidationJson,
        AllowedScopesJson = item.AllowedScopesJson, IsSecret = item.IsSecret
    };

    private static bool IsAllowedScope(string? allowedScopesJson, ScopeType scopeType, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(allowedScopesJson))
            return true;
        try
        {
            var allowed = JsonSerializer.Deserialize<string[]>(allowedScopesJson) ?? [];
            if (allowed.Any(value => Enum.TryParse<ScopeType>(value, true, out var parsed) && parsed == scopeType))
                return true;
            error = $"scope '{scopeType}' is not allowed.";
            return false;
        }
        catch (JsonException)
        {
            error = "allowedScopesJson is invalid JSON.";
            return false;
        }
    }

    private static void ApplyDefinition(SettingDefinition target, ImportDefinition source, DateTimeOffset now)
    {
        target.ApplicationCode = source.ApplicationCode;
        target.DisplayName = source.DisplayName;
        target.Description = source.Description;
        target.Category = source.Category;
        target.DataType = source.DataType;
        target.DefaultValueJson = source.DefaultValueJson;
        target.ValidationJson = source.ValidationJson;
        target.UiSchemaJson = source.UiSchemaJson;
        target.IsSecret = source.IsSecret || source.DataType == SettingDataType.Secret;
        target.AllowedScopesJson = source.AllowedScopesJson;
        target.TagsJson = source.TagsJson;
        target.IsDeprecated = source.IsDeprecated;
        target.UpdatedAt = now;
    }

    private static bool DefinitionMatches(SettingDefinition a, ImportDefinition b) =>
        a.ApplicationCode == b.ApplicationCode && a.DisplayName == b.DisplayName &&
        a.Description == b.Description && a.Category == b.Category && a.DataType == b.DataType &&
        a.DefaultValueJson == b.DefaultValueJson && a.ValidationJson == b.ValidationJson &&
        a.UiSchemaJson == b.UiSchemaJson && a.IsSecret == b.IsSecret &&
        a.AllowedScopesJson == b.AllowedScopesJson && a.TagsJson == b.TagsJson &&
        a.IsDeprecated == b.IsDeprecated;

    private sealed record ImportDocument
    {
        public List<ImportDefinition> Definitions { get; init; } = [];
        public List<ImportAssignment> Assignments { get; init; } = [];
    }

    private sealed record ImportDefinition
    {
        public string Key { get; init; } = string.Empty;
        public string ApplicationCode { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string? Description { get; init; }
        public string? Category { get; init; }
        public SettingDataType DataType { get; init; }
        public string? DefaultValueJson { get; init; }
        public string? ValidationJson { get; init; }
        public string? UiSchemaJson { get; init; }
        public bool IsSecret { get; init; }
        public string? AllowedScopesJson { get; init; }
        public string? TagsJson { get; init; }
        public bool IsDeprecated { get; init; }
    }

    private sealed record ImportAssignment
    {
        public string DefinitionKey { get; init; } = string.Empty;
        public ScopeType ScopeType { get; init; }
        public string? ScopeId { get; init; }
        public string ValueJson { get; init; } = string.Empty;
    }
}
