using Andy.Settings.Application.DTOs.Secrets;
using Andy.Settings.Domain.Enums;

namespace Andy.Settings.Application.Interfaces;

public interface ISecretService
{
    Task<SecretMetadataDto> SetSecretAsync(SetSecretDto dto, string? actorId, CancellationToken ct = default);

    /// <summary>
    /// Decrypts and returns a secret. <paramref name="actorId"/> is recorded on
    /// the resulting <c>SecretRead</c> audit event — pass the caller's identity,
    /// not null.
    /// </summary>
    Task<string?> GetSecretAsync(GetSecretDto dto, string? actorId = null, CancellationToken ct = default);

    Task<SecretMetadataDto> RotateSecretAsync(RotateSecretDto dto, string? actorId, CancellationToken ct = default);
    /// <summary>
    /// Deletes stored secrets for a definition. Pass <paramref name="scopeType"/>
    /// to delete one scope; leave it null to delete every scope for the key.
    /// </summary>
    /// <returns>How many secrets were deleted.</returns>
    Task<int> DeleteSecretAsync(
        string definitionKey,
        ScopeType? scopeType = null,
        string? scopeId = null,
        string? actorId = null,
        CancellationToken ct = default);
}
