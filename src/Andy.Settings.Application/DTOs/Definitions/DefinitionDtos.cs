using System.ComponentModel.DataAnnotations;
using Andy.Settings.Domain.Enums;

namespace Andy.Settings.Application.DTOs.Definitions;

/// <summary>
/// Response DTO for a setting definition.
/// </summary>
public record DefinitionDto(
    Guid Id,
    string Key,
    string ApplicationCode,
    string DisplayName,
    string? Description,
    string? Category,
    SettingDataType DataType,
    string? DefaultValueJson,
    string? ValidationJson,
    string? UiSchemaJson,
    bool IsSecret,
    string? AllowedScopesJson,
    string? TagsJson,
    bool IsDeprecated,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int AssignmentCount
);

/// <summary>
/// Request DTO for creating a setting definition.
/// </summary>
public record CreateDefinitionDto
{
    [Required, StringLength(256)]
    public string Key { get; init; } = string.Empty;

    [Required, StringLength(64)]
    public string ApplicationCode { get; init; } = string.Empty;

    [Required, StringLength(128)]
    public string DisplayName { get; init; } = string.Empty;

    [StringLength(1024)]
    public string? Description { get; init; }

    [StringLength(64)]
    public string? Category { get; init; }

    public SettingDataType DataType { get; init; }
    public string? DefaultValueJson { get; init; }
    public string? ValidationJson { get; init; }
    public string? UiSchemaJson { get; init; }
    public bool IsSecret { get; init; }
    public string? AllowedScopesJson { get; init; }
    public string? TagsJson { get; init; }
}

/// <summary>
/// Request DTO for updating a setting definition.
/// </summary>
public record UpdateDefinitionDto
{
    [StringLength(128)]
    public string? DisplayName { get; init; }

    [StringLength(1024)]
    public string? Description { get; init; }

    [StringLength(64)]
    public string? Category { get; init; }

    public SettingDataType? DataType { get; init; }
    public string? DefaultValueJson { get; init; }
    public string? ValidationJson { get; init; }
    public string? UiSchemaJson { get; init; }
    public bool? IsSecret { get; init; }
    public string? AllowedScopesJson { get; init; }
    public string? TagsJson { get; init; }
    public bool? IsDeprecated { get; init; }
}

/// <summary>
/// Query parameters for searching definitions.
/// </summary>
public record DefinitionQuery
{
    public string? ApplicationCode { get; init; }
    public string? Category { get; init; }
    public string? Search { get; init; }
    public string? Tags { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
}
