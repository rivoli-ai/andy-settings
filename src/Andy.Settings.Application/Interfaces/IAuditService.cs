using Andy.Settings.Application.DTOs.Audit;
using Andy.Settings.Application.DTOs.Common;

namespace Andy.Settings.Application.Interfaces;

public interface IAuditService
{
    Task<PagedResult<AuditEventDto>> QueryAsync(AuditQuery query, CancellationToken ct = default);
    Task<AuditEventDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task RecordAsync(AuditEventDto auditEvent, CancellationToken ct = default);
}
