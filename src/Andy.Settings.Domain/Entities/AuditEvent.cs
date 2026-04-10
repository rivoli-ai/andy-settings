using Andy.Settings.Domain.Enums;

namespace Andy.Settings.Domain.Entities;

/// <summary>
/// An append-only audit record capturing a configuration change.
/// Secret payloads are never stored in <see cref="BeforeJson"/> or <see cref="AfterJson"/>.
/// </summary>
public class AuditEvent
{
    public Guid Id { get; set; }

    /// <summary>
    /// The type of operation that was performed.
    /// </summary>
    public AuditEventType EventType { get; set; }

    /// <summary>
    /// The setting definition key this event relates to.
    /// </summary>
    public string DefinitionKey { get; set; } = string.Empty;

    /// <summary>
    /// The scope level of the affected assignment or secret.
    /// </summary>
    public ScopeType? ScopeType { get; set; }

    /// <summary>
    /// Identifier of the scope target.
    /// </summary>
    public string? ScopeId { get; set; }

    /// <summary>
    /// Type of actor (e.g. "User", "Service", "System").
    /// </summary>
    public string? ActorType { get; set; }

    /// <summary>
    /// Identifier of the actor (user ID or service name).
    /// </summary>
    public string? ActorId { get; set; }

    /// <summary>
    /// JSON representation of the value before the change. Null for creation events.
    /// Secret payloads are excluded.
    /// </summary>
    public string? BeforeJson { get; set; }

    /// <summary>
    /// JSON representation of the value after the change. Null for deletion events.
    /// Secret payloads are excluded.
    /// </summary>
    public string? AfterJson { get; set; }

    /// <summary>
    /// Correlation ID for tracing this event back to the originating request.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// When this event was recorded. Immutable after creation.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}
