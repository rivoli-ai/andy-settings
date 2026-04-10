using System.ComponentModel.DataAnnotations;
using Andy.Settings.Domain.Enums;

namespace Andy.Settings.Application.DTOs.Secrets;

public record SetSecretDto
{
    [Required, StringLength(256)]
    public string DefinitionKey { get; init; } = string.Empty;

    public ScopeType ScopeType { get; init; }
    public string? ScopeId { get; init; }

    [Required]
    public string PlaintextValue { get; init; } = string.Empty;
}

public record GetSecretDto
{
    [Required, StringLength(256)]
    public string DefinitionKey { get; init; } = string.Empty;

    public ScopeType ScopeType { get; init; }
    public string? ScopeId { get; init; }
}

public record RotateSecretDto
{
    [Required, StringLength(256)]
    public string DefinitionKey { get; init; } = string.Empty;

    public ScopeType ScopeType { get; init; }
    public string? ScopeId { get; init; }

    [Required]
    public string NewPlaintextValue { get; init; } = string.Empty;
}

public record SecretMetadataDto(
    Guid Id,
    Guid DefinitionId,
    string DefinitionKey,
    ScopeType ScopeType,
    string? ScopeId,
    string? UpdatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);
