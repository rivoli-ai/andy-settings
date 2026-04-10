using Andy.Settings.Domain.Enums;

namespace Andy.Settings.Application.DTOs.Audit;

/// <summary>
/// Response DTO for an audit event.
/// </summary>
public record AuditEventDto(
    Guid Id,
    AuditEventType EventType,
    string DefinitionKey,
    ScopeType? ScopeType,
    string? ScopeId,
    string? ActorType,
    string? ActorId,
    string? BeforeJson,
    string? AfterJson,
    string? CorrelationId,
    DateTimeOffset CreatedAt
);

/// <summary>
/// Query parameters for searching audit events.
/// </summary>
public record AuditQuery
{
    public string? DefinitionKey { get; init; }
    public DateTimeOffset? DateFrom { get; init; }
    public DateTimeOffset? DateTo { get; init; }
    public string? ActorId { get; init; }
    public AuditEventType? EventType { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
}
