using Andy.Settings.Application.DTOs.Secrets;

namespace Andy.Settings.Application.Interfaces;

public interface ISecretService
{
    Task<SecretMetadataDto> SetSecretAsync(SetSecretDto dto, string? actorId, CancellationToken ct = default);
    Task<string?> GetSecretAsync(GetSecretDto dto, CancellationToken ct = default);
    Task<SecretMetadataDto> RotateSecretAsync(RotateSecretDto dto, string? actorId, CancellationToken ct = default);
    Task DeleteSecretAsync(string definitionKey, CancellationToken ct = default);
}
