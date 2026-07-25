using Andy.Settings.Application.DTOs.Secrets;

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
    Task DeleteSecretAsync(string definitionKey, string? actorId = null, CancellationToken ct = default);
}
