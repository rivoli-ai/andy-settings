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

    /// <summary>Settings were imported from an external source.</summary>
    Imported,

    /// <summary>Settings were exported.</summary>
    Exported,

    /// <summary>An RBAC permission check was denied.</summary>
    AccessDenied
}
