using Andy.Settings.Application.DTOs.Common;
using Andy.Settings.Application.DTOs.Values;
using Andy.Settings.Domain.Enums;

namespace Andy.Settings.Application.Interfaces;

public interface IAssignmentService
{
    Task<AssignmentDto> SetAsync(SetValueDto dto, string? actorId, CancellationToken ct = default);
    Task DeleteAsync(Guid id, string? actorId, CancellationToken ct = default);
    Task<PagedResult<AssignmentDto>> ListByScopeAsync(string? definitionKey, ScopeType? scopeType, string? scopeId, int page, int pageSize, CancellationToken ct = default);
    Task BulkSetAsync(IEnumerable<SetValueDto> dtos, string? actorId, CancellationToken ct = default);
}
