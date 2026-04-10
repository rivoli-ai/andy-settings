using Andy.Settings.Application.DTOs.Common;
using Andy.Settings.Application.DTOs.Values;
using Andy.Settings.Application.Interfaces;
using Andy.Settings.Domain.Entities;
using Andy.Settings.Domain.Enums;
using Andy.Settings.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Andy.Settings.Infrastructure.Repositories;

public class AssignmentRepository : IAssignmentService
{
    private readonly SettingsDbContext _db;
    private readonly IAuditService _audit;

    public AssignmentRepository(SettingsDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<AssignmentDto> SetAsync(SetValueDto dto, string? actorId, CancellationToken ct = default)
    {
        var definition = await _db.SettingDefinitions.FirstOrDefaultAsync(d => d.Key == dto.DefinitionKey, ct)
            ?? throw new KeyNotFoundException($"Definition '{dto.DefinitionKey}' not found.");

        var existing = await _db.SettingAssignments
            .FirstOrDefaultAsync(a =>
                a.DefinitionId == definition.Id &&
                a.ScopeType == dto.ScopeType &&
                a.ScopeId == dto.ScopeId, ct);

        if (existing is not null)
        {
            if (dto.Etag is not null && dto.Etag != existing.Etag)
                throw new InvalidOperationException("Concurrency conflict: etag mismatch.");

            var beforeJson = existing.ValueJson;
            existing.ValueJson = dto.ValueJson;
            existing.Etag = Guid.NewGuid().ToString("N");
            existing.Version++;
            existing.UpdatedBy = actorId;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);

            await _audit.RecordAsync(new(Guid.NewGuid(), AuditEventType.Updated,
                dto.DefinitionKey, dto.ScopeType, dto.ScopeId, "User", actorId,
                beforeJson, dto.ValueJson, null, DateTimeOffset.UtcNow), ct);

            return ToDto(existing, dto.DefinitionKey);
        }

        var entity = new SettingAssignment
        {
            Id = Guid.NewGuid(),
            DefinitionId = definition.Id,
            ScopeType = dto.ScopeType,
            ScopeId = dto.ScopeId,
            ValueJson = dto.ValueJson,
            Etag = Guid.NewGuid().ToString("N"),
            Version = 1,
            UpdatedBy = actorId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.SettingAssignments.Add(entity);
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAsync(new(Guid.NewGuid(), AuditEventType.Created,
            dto.DefinitionKey, dto.ScopeType, dto.ScopeId, "User", actorId,
            null, dto.ValueJson, null, DateTimeOffset.UtcNow), ct);

        return ToDto(entity, dto.DefinitionKey);
    }

    public async Task DeleteAsync(Guid id, string? actorId, CancellationToken ct = default)
    {
        var entity = await _db.SettingAssignments
            .Include(a => a.Definition)
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new KeyNotFoundException($"Assignment '{id}' not found.");

        _db.SettingAssignments.Remove(entity);
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAsync(new(Guid.NewGuid(), AuditEventType.Deleted,
            entity.Definition.Key, entity.ScopeType, entity.ScopeId, "User", actorId,
            entity.ValueJson, null, null, DateTimeOffset.UtcNow), ct);
    }

    public async Task<PagedResult<AssignmentDto>> ListByScopeAsync(
        string? definitionKey, ScopeType? scopeType, string? scopeId,
        int page, int pageSize, CancellationToken ct = default)
    {
        var q = _db.SettingAssignments.Include(a => a.Definition).AsQueryable();

        if (!string.IsNullOrEmpty(definitionKey))
            q = q.Where(a => a.Definition.Key == definitionKey);
        if (scopeType.HasValue)
            q = q.Where(a => a.ScopeType == scopeType.Value);
        if (!string.IsNullOrEmpty(scopeId))
            q = q.Where(a => a.ScopeId == scopeId);

        var totalCount = await q.CountAsync(ct);
        var items = await q
            .OrderBy(a => a.Definition.Key).ThenBy(a => a.ScopeType)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<AssignmentDto>(
            items.Select(a => ToDto(a, a.Definition.Key)).ToList(),
            totalCount, page, pageSize);
    }

    public async Task BulkSetAsync(IEnumerable<SetValueDto> dtos, string? actorId, CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        foreach (var dto in dtos)
            await SetAsync(dto, actorId, ct);
        await transaction.CommitAsync(ct);
    }

    private static AssignmentDto ToDto(SettingAssignment e, string definitionKey) => new(
        e.Id, e.DefinitionId, definitionKey, e.ScopeType, e.ScopeId,
        e.ValueJson, e.Etag, e.Version, e.UpdatedBy, e.CreatedAt, e.UpdatedAt);
}
