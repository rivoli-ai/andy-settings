using Andy.Settings.Application.DTOs.Common;
using Andy.Settings.Application.Exceptions;
using Andy.Settings.Application.DTOs.Definitions;
using Andy.Settings.Application.Interfaces;
using Andy.Settings.Application.Messaging.Events;
using Andy.Settings.Domain.Entities;
using Andy.Settings.Domain.Enums;
using Andy.Settings.Infrastructure.Data;
using Andy.Settings.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Andy.Settings.Infrastructure.Repositories;

public class DefinitionRepository : IDefinitionService
{
    private readonly SettingsDbContext _db;
    private readonly IAuditService _audit;

    public DefinitionRepository(SettingsDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<DefinitionDto?> GetAsync(string key, CancellationToken ct = default)
    {
        var entity = await _db.SettingDefinitions
            .Include(d => d.Assignments)
            .FirstOrDefaultAsync(d => d.Key == key, ct);

        return entity is null ? null : ToDto(entity);
    }

    public async Task<PagedResult<DefinitionDto>> SearchAsync(DefinitionQuery query, CancellationToken ct = default)
    {
        var q = _db.SettingDefinitions.Include(d => d.Assignments).AsQueryable();

        if (!string.IsNullOrEmpty(query.ApplicationCode))
            q = q.Where(d => d.ApplicationCode == query.ApplicationCode);

        if (!string.IsNullOrEmpty(query.Category))
            q = q.Where(d => d.Category == query.Category);

        if (!string.IsNullOrEmpty(query.Search))
        {
            var search = query.Search.ToLowerInvariant();
            q = q.Where(d =>
                d.Key.ToLower().Contains(search) ||
                d.DisplayName.ToLower().Contains(search) ||
                (d.Description != null && d.Description.ToLower().Contains(search)));
        }

        if (!string.IsNullOrEmpty(query.Tags))
        {
            var tag = query.Tags.ToLowerInvariant();
            q = q.Where(d => d.TagsJson != null && d.TagsJson.ToLower().Contains(tag));
        }

        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);

        var totalCount = await q.CountAsync(ct);

        var items = await q
            .OrderBy(d => d.ApplicationCode).ThenBy(d => d.Key)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<DefinitionDto>(
            items.Select(ToDto).ToList(),
            totalCount,
            page,
            pageSize);
    }

    public async Task<IReadOnlyList<string>> GetCategoriesAsync(
        string? applicationCode = null, CancellationToken ct = default)
    {
        var q = _db.SettingDefinitions.AsNoTracking();

        if (!string.IsNullOrEmpty(applicationCode))
            q = q.Where(d => d.ApplicationCode == applicationCode);

        // DISTINCT server-side, so the result is complete regardless of how
        // large the catalog grows.
        return await q
            .Where(d => d.Category != null && d.Category != "")
            .Select(d => d.Category!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(ct);
    }

    public async Task<DefinitionDto> CreateAsync(CreateDefinitionDto dto, string? actorId = null, CancellationToken ct = default)
    {
        var existing = await _db.SettingDefinitions.AnyAsync(d => d.Key == dto.Key, ct);
        if (existing)
            throw new DuplicateKeyException($"Definition with key '{dto.Key}' already exists.");

        if ((dto.IsSecret || dto.DataType == SettingDataType.Secret) && dto.DefaultValueJson is not null)
            throw new InvalidOperationException("Secret definitions cannot contain plaintext default values.");

        var entity = new SettingDefinition
        {
            Id = Guid.NewGuid(),
            Key = dto.Key,
            ApplicationCode = dto.ApplicationCode,
            DisplayName = dto.DisplayName,
            Description = dto.Description,
            Category = dto.Category,
            DataType = dto.DataType,
            DefaultValueJson = dto.DefaultValueJson,
            ValidationJson = dto.ValidationJson,
            UiSchemaJson = dto.UiSchemaJson,
            IsSecret = dto.IsSecret || dto.DataType == SettingDataType.Secret,
            AllowedScopesJson = dto.AllowedScopesJson,
            TagsJson = dto.TagsJson,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.SettingDefinitions.Add(entity);
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAsync(new(Guid.NewGuid(), AuditEventType.Created,
            entity.Key, null, null, "User", actorId,
            null, null, null, DateTimeOffset.UtcNow), ct);

        return ToDto(entity);
    }

    public async Task<DefinitionDto> UpdateAsync(string key, UpdateDefinitionDto dto, string? actorId = null, CancellationToken ct = default)
    {
        var entity = await _db.SettingDefinitions
            .Include(d => d.Assignments)
            .Include(d => d.Secrets)
            .FirstOrDefaultAsync(d => d.Key == key, ct)
            ?? throw new KeyNotFoundException($"Definition '{key}' not found.");

        var wasSecret = entity.IsSecret || entity.DataType == SettingDataType.Secret;

        if (dto.DisplayName is not null) entity.DisplayName = dto.DisplayName;
        if (dto.Description is not null) entity.Description = dto.Description;
        if (dto.Category is not null) entity.Category = dto.Category;
        if (dto.DataType.HasValue) entity.DataType = dto.DataType.Value;
        if (dto.DefaultValueJson is not null) entity.DefaultValueJson = dto.DefaultValueJson;
        if (dto.ValidationJson is not null) entity.ValidationJson = dto.ValidationJson;
        if (dto.UiSchemaJson is not null) entity.UiSchemaJson = dto.UiSchemaJson;
        if (dto.IsSecret.HasValue) entity.IsSecret = dto.IsSecret.Value;
        if (dto.AllowedScopesJson is not null) entity.AllowedScopesJson = dto.AllowedScopesJson;
        if (dto.TagsJson is not null) entity.TagsJson = dto.TagsJson;
        if (dto.IsDeprecated.HasValue) entity.IsDeprecated = dto.IsDeprecated.Value;
        if (entity.DataType == SettingDataType.Secret)
            entity.IsSecret = true;
        var isSecret = entity.IsSecret || entity.DataType == SettingDataType.Secret;
        if (wasSecret != isSecret && (entity.Assignments.Count > 0 || entity.Secrets.Count > 0))
            throw new InvalidOperationException(
                "A definition with stored values cannot be switched between secret and ordinary storage.");
        if (isSecret && entity.DefaultValueJson is not null)
            throw new InvalidOperationException("Secret definitions cannot contain plaintext default values.");
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _audit.RecordAsync(new(Guid.NewGuid(), AuditEventType.Updated,
            entity.Key, null, null, "User", actorId,
            null, null, null, DateTimeOffset.UtcNow), ct);

        return ToDto(entity);
    }

    /// <summary>
    /// Deletes a definition and everything stored under it.
    /// </summary>
    /// <remarks>
    /// The cascade configured in <c>SettingsDbContext</c> removes every
    /// <c>SettingAssignment</c> AND every <c>EncryptedSecret</c> for this key.
    /// That used to happen in total silence: no audit row, and no event, so
    /// downstream consumers kept serving values that no longer existed —
    /// indefinitely, since they invalidate on events
    /// (rivoli-ai/andy-settings#135).
    ///
    /// Rather than invent a definition-level event family, the cascade is
    /// announced with the contracts consumers already handle: one
    /// <c>config.*.deleted</c> per assignment that is about to disappear, and a
    /// secret-deleted event when the key had stored secrets. That is strictly
    /// more precise than a single definition-level event — consumers learn
    /// exactly which key/scope pairs died.
    /// </remarks>
    public async Task DeleteAsync(string key, string? actorId = null, CancellationToken ct = default)
    {
        var entity = await _db.SettingDefinitions
            .Include(d => d.Assignments)
            .Include(d => d.Secrets)
            .FirstOrDefaultAsync(d => d.Key == key, ct)
            ?? throw new KeyNotFoundException($"Definition '{key}' not found.");

        // Events are appended BEFORE the removal so the assignment rows are
        // still loaded, and land in the same SaveChanges as the delete itself
        // (ADR 0001 §3) — rolling back the delete rolls back the events.
        foreach (var assignment in entity.Assignments)
            _db.AppendConfigChanged(assignment, entity, ConfigEventKind.Deleted, newValueJson: null);

        if (entity.Secrets.Count > 0)
        {
            _db.AppendSecretChanged(
                definitionKey: entity.Key,
                definitionId: entity.Id,
                scopeType: ScopeType.Machine,
                scopeId: null,
                kind: SecretEventKind.Deleted);
        }

        _db.SettingDefinitions.Remove(entity);
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAsync(new(Guid.NewGuid(), AuditEventType.Deleted,
            key, null, null, "User", actorId,
            null, null, null, DateTimeOffset.UtcNow), ct);
    }

    private static DefinitionDto ToDto(SettingDefinition e) => new(
        e.Id, e.Key, e.ApplicationCode, e.DisplayName, e.Description,
        e.Category, e.DataType, e.DefaultValueJson, e.ValidationJson,
        e.UiSchemaJson, e.IsSecret, e.AllowedScopesJson, e.TagsJson,
        e.IsDeprecated, e.CreatedAt, e.UpdatedAt,
        e.Assignments?.Count ?? 0);
}
