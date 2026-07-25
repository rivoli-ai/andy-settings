using Andy.Settings.Application.DTOs.Audit;
using Andy.Settings.Application.DTOs.Common;
using Andy.Settings.Application.Interfaces;
using Andy.Settings.Domain.Entities;
using Andy.Settings.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Andy.Settings.Infrastructure.Repositories;

/// <summary>
/// Append-only audit repository. No Update or Delete methods.
/// </summary>
public class AuditRepository : IAuditService
{
    private readonly SettingsDbContext _db;

    public AuditRepository(SettingsDbContext db) => _db = db;

    public async Task<PagedResult<AuditEventDto>> QueryAsync(AuditQuery query, CancellationToken ct = default)
    {
        // Every filter, the sort, and the paging run server-side.
        //
        // This used to materialize the whole table and finish the work in
        // LINQ-to-Objects, on the grounds that "SQLite has limited support for
        // DateTimeOffset and enum conversions". Both premises are obsolete:
        // SettingsDbContext.ConfigureConventions binds DateTimeOffsetToBinaryConverter
        // for SQLite precisely so these predicates translate, and EventType is
        // configured with HasConversion<string>(). The audit table is append-only
        // with no retention job, so the in-memory path degraded without bound
        // (rivoli-ai/andy-settings#133).
        var q = _db.AuditEvents.AsNoTracking();

        if (!string.IsNullOrEmpty(query.DefinitionKey))
            q = q.Where(e => e.DefinitionKey == query.DefinitionKey);
        if (!string.IsNullOrEmpty(query.ActorId))
            q = q.Where(e => e.ActorId == query.ActorId);
        if (query.EventType.HasValue)
            q = q.Where(e => e.EventType == query.EventType.Value);
        if (query.DateFrom.HasValue)
            q = q.Where(e => e.CreatedAt >= query.DateFrom.Value);
        if (query.DateTo.HasValue)
            q = q.Where(e => e.CreatedAt <= query.DateTo.Value);

        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);

        var totalCount = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<AuditEventDto>(
            items.Select(ToDto).ToList(),
            totalCount, page, pageSize);
    }

    public async Task<AuditEventDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.AuditEvents.FindAsync([id], ct);
        return entity is null ? null : ToDto(entity);
    }

    public async Task RecordAsync(AuditEventDto dto, CancellationToken ct = default)
    {
        var entity = new AuditEvent
        {
            Id = dto.Id == Guid.Empty ? Guid.NewGuid() : dto.Id,
            EventType = dto.EventType,
            DefinitionKey = dto.DefinitionKey,
            ScopeType = dto.ScopeType,
            ScopeId = dto.ScopeId,
            ActorType = dto.ActorType,
            ActorId = dto.ActorId,
            BeforeJson = dto.BeforeJson,
            AfterJson = dto.AfterJson,
            CorrelationId = dto.CorrelationId,
            CreatedAt = dto.CreatedAt == default ? DateTimeOffset.UtcNow : dto.CreatedAt
        };

        _db.AuditEvents.Add(entity);
        await _db.SaveChangesAsync(ct);
    }

    private static AuditEventDto ToDto(AuditEvent e) => new(
        e.Id, e.EventType, e.DefinitionKey, e.ScopeType, e.ScopeId,
        e.ActorType, e.ActorId, e.BeforeJson, e.AfterJson,
        e.CorrelationId, e.CreatedAt);
}
