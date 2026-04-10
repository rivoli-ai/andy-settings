using System.Text.Json.Serialization;

namespace Andy.Settings.Domain.Enums;

/// <summary>
/// The scope level at which a setting value is assigned.
/// Listed in ascending precedence order — higher values win during resolution.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ScopeType
{
    /// <summary>Machine-wide default (lowest precedence).</summary>
    Machine = 0,

    /// <summary>Application-level setting.</summary>
    Application = 1,

    /// <summary>Service-level setting within an application.</summary>
    Service = 2,

    /// <summary>Per-user setting.</summary>
    User = 3,

    /// <summary>Per-team setting.</summary>
    Team = 4,

    /// <summary>Per-workspace setting.</summary>
    Workspace = 5,

    /// <summary>Runtime override (highest precedence).</summary>
    RuntimeOverride = 6
}
