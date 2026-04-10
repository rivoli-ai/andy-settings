using Andy.Settings.Domain.Enums;

namespace Andy.Settings.Domain.Entities;

/// <summary>
/// A setting definition describes the schema for a configuration value:
/// its key, data type, validation rules, default, and allowed scopes.
/// </summary>
public class SettingDefinition
{
    public Guid Id { get; set; }

    /// <summary>
    /// Unique dot-separated key within an application code (e.g. "andy.containers.defaultProvider").
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// The application that owns this definition (e.g. "containers", "codeindex").
    /// </summary>
    public string ApplicationCode { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name shown in the UI.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Longer description of what this setting controls.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Logical category for grouping (e.g. "General", "Security", "Database").
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// The value type for this setting.
    /// </summary>
    public SettingDataType DataType { get; set; }

    /// <summary>
    /// Default value as a JSON string. Used when no assignment exists at any scope.
    /// </summary>
    public string? DefaultValueJson { get; set; }

    /// <summary>
    /// JSON Schema for validating values assigned to this setting.
    /// </summary>
    public string? ValidationJson { get; set; }

    /// <summary>
    /// UI rendering hints as JSON (e.g. control type, placeholder, options list for enums).
    /// </summary>
    public string? UiSchemaJson { get; set; }

    /// <summary>
    /// When true, values for this setting are stored as encrypted secrets.
    /// </summary>
    public bool IsSecret { get; set; }

    /// <summary>
    /// JSON array of <see cref="ScopeType"/> values indicating where assignments are allowed.
    /// </summary>
    public string? AllowedScopesJson { get; set; }

    /// <summary>
    /// JSON array of tags for filtering and discovery.
    /// </summary>
    public string? TagsJson { get; set; }

    /// <summary>
    /// When true, the setting is deprecated and should not be used for new assignments.
    /// </summary>
    public bool IsDeprecated { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation properties
    public ICollection<SettingAssignment> Assignments { get; set; } = new List<SettingAssignment>();
    public ICollection<EncryptedSecret> Secrets { get; set; } = new List<EncryptedSecret>();
}
