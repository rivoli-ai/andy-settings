using Andy.Settings.Application.DTOs.Audit;
using Andy.Settings.Application.DTOs.Secrets;
using Andy.Settings.Application.Interfaces;
using Andy.Settings.Domain.Entities;
using Andy.Settings.Domain.Enums;
using Andy.Settings.Infrastructure.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Andy.Settings.Infrastructure.Services;

public class SecretService : ISecretService
{
    private readonly SettingsDbContext _db;
    private readonly IDataProtector _protector;
    private readonly IAuditService _audit;

    public SecretService(SettingsDbContext db, IDataProtectionProvider dataProtectionProvider, IAuditService audit)
    {
        _db = db;
        _protector = dataProtectionProvider.CreateProtector("AndySettings.Secrets");
        _audit = audit;
    }

    public async Task<SecretMetadataDto> SetSecretAsync(SetSecretDto dto, string? actorId, CancellationToken ct = default)
    {
        var definition = await _db.SettingDefinitions.FirstOrDefaultAsync(d => d.Key == dto.DefinitionKey, ct)
            ?? throw new KeyNotFoundException($"Definition '{dto.DefinitionKey}' not found.");

        if (!definition.IsSecret)
            throw new InvalidOperationException($"Definition '{dto.DefinitionKey}' is not a secret-type setting.");

        var encrypted = _protector.Protect(dto.PlaintextValue);

        var existing = await _db.EncryptedSecrets
            .FirstOrDefaultAsync(s =>
                s.DefinitionId == definition.Id &&
                s.ScopeType == dto.ScopeType &&
                s.ScopeId == dto.ScopeId, ct);

        if (existing is not null)
        {
            existing.EncryptedValue = encrypted;
            existing.UpdatedBy = actorId;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            existing = new EncryptedSecret
            {
                Id = Guid.NewGuid(),
                DefinitionId = definition.Id,
                ScopeType = dto.ScopeType,
                ScopeId = dto.ScopeId,
                EncryptedValue = encrypted,
                UpdatedBy = actorId,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _db.EncryptedSecrets.Add(existing);
        }

        await _db.SaveChangesAsync(ct);

        await _audit.RecordAsync(new AuditEventDto(
            Guid.NewGuid(), AuditEventType.SecretRotated, dto.DefinitionKey,
            dto.ScopeType, dto.ScopeId, "User", actorId,
            null, null, null, DateTimeOffset.UtcNow), ct);

        return ToMetadataDto(existing, dto.DefinitionKey);
    }

    public async Task<string?> GetSecretAsync(GetSecretDto dto, CancellationToken ct = default)
    {
        var definition = await _db.SettingDefinitions.FirstOrDefaultAsync(d => d.Key == dto.DefinitionKey, ct)
            ?? throw new KeyNotFoundException($"Definition '{dto.DefinitionKey}' not found.");

        var secret = await _db.EncryptedSecrets
            .FirstOrDefaultAsync(s =>
                s.DefinitionId == definition.Id &&
                s.ScopeType == dto.ScopeType &&
                s.ScopeId == dto.ScopeId, ct);

        if (secret is null)
            return null;

        return _protector.Unprotect(secret.EncryptedValue);
    }

    public async Task<SecretMetadataDto> RotateSecretAsync(RotateSecretDto dto, string? actorId, CancellationToken ct = default)
    {
        return await SetSecretAsync(new SetSecretDto
        {
            DefinitionKey = dto.DefinitionKey,
            ScopeType = dto.ScopeType,
            ScopeId = dto.ScopeId,
            PlaintextValue = dto.NewPlaintextValue
        }, actorId, ct);
    }

    public async Task DeleteSecretAsync(string definitionKey, CancellationToken ct = default)
    {
        var definition = await _db.SettingDefinitions.FirstOrDefaultAsync(d => d.Key == definitionKey, ct)
            ?? throw new KeyNotFoundException($"Definition '{definitionKey}' not found.");

        var secrets = await _db.EncryptedSecrets
            .Where(s => s.DefinitionId == definition.Id)
            .ToListAsync(ct);

        _db.EncryptedSecrets.RemoveRange(secrets);
        await _db.SaveChangesAsync(ct);
    }

    private static SecretMetadataDto ToMetadataDto(EncryptedSecret e, string definitionKey) => new(
        e.Id, e.DefinitionId, definitionKey, e.ScopeType, e.ScopeId,
        e.UpdatedBy, e.CreatedAt, e.UpdatedAt);
}
