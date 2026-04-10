using Andy.Settings.Domain.Enums;

namespace Andy.Settings.Domain.Entities;

/// <summary>
/// A setting assignment stores a concrete value at a specific scope.
/// Multiple assignments can exist for the same definition at different scopes;
/// the resolution engine determines which one wins.
/// </summary>
public class SettingAssignment
{
    public Guid Id { get; set; }

    /// <summary>
    /// The definition this assignment provides a value for.
    /// </summary>
    public Guid DefinitionId { get; set; }

    /// <summary>
    /// The scope level of this assignment (e.g. User, Team, Machine).
    /// </summary>
    public ScopeType ScopeType { get; set; }

    /// <summary>
    /// Identifier of the scope target (e.g. user ID, team ID). Null for Machine scope.
    /// </summary>
    public string? ScopeId { get; set; }

    /// <summary>
    /// The assigned value as a JSON string.
    /// </summary>
    public string ValueJson { get; set; } = string.Empty;

    /// <summary>
    /// Concurrency token for optimistic locking. Clients must send the current
    /// etag on updates; a mismatch results in 409 Conflict.
    /// </summary>
    public string Etag { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Monotonically increasing version number, incremented on each update.
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Identity of the user who last set this value (from JWT claims).
    /// </summary>
    public string? UpdatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation property
    public SettingDefinition Definition { get; set; } = null!;
}
