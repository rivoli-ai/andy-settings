using System.ComponentModel.DataAnnotations;
using Andy.Settings.Domain.Enums;

namespace Andy.Settings.Application.DTOs.Effective;

/// <summary>
/// Context for resolving effective setting values.
/// </summary>
public record ResolutionContext
{
    public string? UserId { get; init; }
    public string? TeamId { get; init; }
    public string? WorkspaceId { get; init; }
    public string? ApplicationCode { get; init; }
    public string? ServiceCode { get; init; }
}

/// <summary>
/// Request to resolve a single effective value.
/// </summary>
public record ResolveRequest
{
    [Required, StringLength(256)]
    public string Key { get; init; } = string.Empty;

    [Required]
    public ResolutionContext Context { get; init; } = new();
}

/// <summary>
/// Request to resolve multiple effective values.
/// </summary>
public record ResolveBatchRequest
{
    [Required]
    public IReadOnlyList<string> Keys { get; init; } = [];

    [Required]
    public ResolutionContext Context { get; init; } = new();
}

/// <summary>
/// A single entry in the resolution source chain.
/// </summary>
public record SourceChainEntry(
    ScopeType ScopeType,
    string? ScopeId,
    string? ValueJson,
    bool IsWinner
);

/// <summary>
/// The result of resolving an effective setting value.
/// </summary>
public record ResolvedSetting
{
    public string Key { get; init; } = string.Empty;
    public string? EffectiveValue { get; init; }
    public ScopeType? WinningScopeType { get; init; }
    public string? WinningScopeId { get; init; }
    public SettingDataType DataType { get; init; }
    public bool IsSecret { get; init; }
    public bool IsDefault { get; init; }
    public IReadOnlyList<SourceChainEntry> SourceChain { get; init; } = [];
    public bool IsValid { get; init; } = true;
    public string? ValidationMessage { get; init; }
}
