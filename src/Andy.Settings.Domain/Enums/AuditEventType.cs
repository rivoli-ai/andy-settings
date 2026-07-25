using System.Text.Json.Serialization;

namespace Andy.Settings.Domain.Enums;

/// <summary>
/// The type of operation recorded in the audit trail.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AuditEventType
{
    /// <summary>A setting value or definition was created.</summary>
    Created,

    /// <summary>A setting value or definition was updated.</summary>
    Updated,

    /// <summary>A setting value or definition was deleted.</summary>
    Deleted,

    /// <summary>An encrypted secret was rotated.</summary>
    SecretRotated,

    /// <summary>
    /// An encrypted secret was decrypted and returned to a caller. Recorded
    /// with the definition key, scope, and actor — never the value or any
    /// digest of it, which would let an attacker with audit access confirm a
    /// guessed secret.
    /// </summary>
    SecretRead,

    /// <summary>All secrets for a definition were deleted.</summary>
    SecretDeleted,

    /// <summary>Settings were imported from an external source.</summary>
    Imported,

    /// <summary>Settings were exported.</summary>
    Exported,

    /// <summary>An RBAC permission check was denied.</summary>
    AccessDenied
}
