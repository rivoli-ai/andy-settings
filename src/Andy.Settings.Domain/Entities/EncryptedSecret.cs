using Andy.Settings.Domain.Enums;

namespace Andy.Settings.Domain.Entities;

/// <summary>
/// Stores an encrypted secret value for a setting definition at a specific scope.
/// The value is encrypted using ASP.NET Core Data Protection API (AES-256-GCM)
/// and is only decrypted for users with the <c>secret:read</c> RBAC permission.
/// </summary>
public class EncryptedSecret
{
    public Guid Id { get; set; }

    /// <summary>
    /// The definition this secret belongs to. The definition must have <c>IsSecret = true</c>.
    /// </summary>
    public Guid DefinitionId { get; set; }

    /// <summary>
    /// The scope level of this secret.
    /// </summary>
    public ScopeType ScopeType { get; set; }

    /// <summary>
    /// Identifier of the scope target. Null for Machine scope.
    /// </summary>
    public string? ScopeId { get; set; }

    /// <summary>
    /// The encrypted value produced by <c>IDataProtector.Protect()</c>.
    /// Never stored or transmitted in plaintext.
    /// </summary>
    public string EncryptedValue { get; set; } = string.Empty;

    /// <summary>
    /// Identity of the user who last set or rotated this secret.
    /// </summary>
    public string? UpdatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation property
    public SettingDefinition Definition { get; set; } = null!;
}
