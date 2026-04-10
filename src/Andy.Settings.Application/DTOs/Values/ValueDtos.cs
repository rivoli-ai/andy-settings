using System.ComponentModel.DataAnnotations;
using Andy.Settings.Domain.Enums;

namespace Andy.Settings.Application.DTOs.Values;

/// <summary>
/// Response DTO for a setting assignment.
/// </summary>
public record AssignmentDto(
    Guid Id,
    Guid DefinitionId,
    string DefinitionKey,
    ScopeType ScopeType,
    string? ScopeId,
    string ValueJson,
    string Etag,
    int Version,
    string? UpdatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

/// <summary>
/// Request DTO for setting a value (upsert).
/// </summary>
public record SetValueDto
{
    [Required, StringLength(256)]
    public string DefinitionKey { get; init; } = string.Empty;

    public ScopeType ScopeType { get; init; }
    public string? ScopeId { get; init; }

    [Required]
    public string ValueJson { get; init; } = string.Empty;

    /// <summary>
    /// For updates: the current etag. Omit for new assignments.
    /// </summary>
    public string? Etag { get; init; }
}

/// <summary>
/// Request DTO for deleting a value.
/// </summary>
public record DeleteValueDto
{
    public Guid Id { get; init; }
}
