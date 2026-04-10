using Andy.Settings.Application.DTOs.Common;
using Andy.Settings.Application.DTOs.Definitions;
using Andy.Settings.Application.Interfaces;
using Andy.Settings.Domain.Entities;
using Andy.Settings.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Andy.Settings.Infrastructure.Repositories;

public class DefinitionRepository : IDefinitionService
{
    private readonly SettingsDbContext _db;

    public DefinitionRepository(SettingsDbContext db) => _db = db;

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

        var totalCount = await q.CountAsync(ct);

        var items = await q
            .OrderBy(d => d.ApplicationCode).ThenBy(d => d.Key)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PagedResult<DefinitionDto>(
            items.Select(ToDto).ToList(),
            totalCount,
            query.Page,
            query.PageSize);
    }

    public async Task<DefinitionDto> CreateAsync(CreateDefinitionDto dto, CancellationToken ct = default)
    {
        var existing = await _db.SettingDefinitions
            .AnyAsync(d => d.Key == dto.Key && d.ApplicationCode == dto.ApplicationCode, ct);
        if (existing)
            throw new InvalidOperationException($"Definition with key '{dto.Key}' already exists for application '{dto.ApplicationCode}'.");

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
            IsSecret = dto.IsSecret,
            AllowedScopesJson = dto.AllowedScopesJson,
            TagsJson = dto.TagsJson,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.SettingDefinitions.Add(entity);
        await _db.SaveChangesAsync(ct);

        return ToDto(entity);
    }

    public async Task<DefinitionDto> UpdateAsync(string key, UpdateDefinitionDto dto, CancellationToken ct = default)
    {
        var entity = await _db.SettingDefinitions
            .Include(d => d.Assignments)
            .FirstOrDefaultAsync(d => d.Key == key, ct)
            ?? throw new KeyNotFoundException($"Definition '{key}' not found.");

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
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var entity = await _db.SettingDefinitions.FirstOrDefaultAsync(d => d.Key == key, ct)
            ?? throw new KeyNotFoundException($"Definition '{key}' not found.");

        _db.SettingDefinitions.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }

    private static DefinitionDto ToDto(SettingDefinition e) => new(
        e.Id, e.Key, e.ApplicationCode, e.DisplayName, e.Description,
        e.Category, e.DataType, e.DefaultValueJson, e.ValidationJson,
        e.UiSchemaJson, e.IsSecret, e.AllowedScopesJson, e.TagsJson,
        e.IsDeprecated, e.CreatedAt, e.UpdatedAt,
        e.Assignments?.Count ?? 0);
}
