using System.Text.Json.Serialization;

namespace Andy.Settings.Domain.Enums;

/// <summary>
/// The data type of a setting definition's value.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SettingDataType
{
    /// <summary>Plain text value.</summary>
    String,

    /// <summary>Whole number value.</summary>
    Integer,

    /// <summary>True/false value.</summary>
    Boolean,

    /// <summary>Floating-point number value.</summary>
    Decimal,

    /// <summary>Value from a predefined set of options.</summary>
    Enum,

    /// <summary>Time duration (e.g. "00:05:00").</summary>
    Duration,

    /// <summary>URI/URL value.</summary>
    Uri,

    /// <summary>Arbitrary JSON object.</summary>
    Json,

    /// <summary>Comma-separated or JSON array of strings.</summary>
    StringList,

    /// <summary>Encrypted secret value. Stored via Data Protection API.</summary>
    Secret
}
