using Andy.Settings.Application.DTOs.Audit;
using Andy.Settings.Application.DTOs.Secrets;
using Andy.Settings.Application.Interfaces;
using Andy.Settings.Application.Messaging.Events;
using Andy.Settings.Domain.Entities;
using Andy.Settings.Domain.Enums;
using Andy.Settings.Infrastructure.Data;
using Andy.Settings.Infrastructure.Messaging;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace Andy.Settings.Infrastructure.Services;

public class SecretService : ISecretService
{
    private readonly SettingsDbContext _db;
    private readonly IDataProtector _protector;
    private readonly IAuditService _audit;
    private readonly ILogger<SecretService> _logger;

    public SecretService(SettingsDbContext db, IDataProtectionProvider dataProtectionProvider, IAuditService audit, ILogger<SecretService> logger)
    {
        _db = db;
        _protector = dataProtectionProvider.CreateProtector("AndySettings.Secrets");
        _audit = audit;
        _logger = logger;
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

        // rivoli-ai/conductor#925 (M1.2.1). A first-time write is
        // `Set`; an overwrite is `Rotated`. Both carry the same
        // subject (`.updated`) on the wire so existing consumers
        // (andy-models' SettingsChangeConsumer) don't need to subscribe
        // to two patterns; the distinct kind survives in the payload's
        // `Mutation` field for consumers that care.
        SecretEventKind mutationKind;
        if (existing is not null)
        {
            existing.EncryptedValue = encrypted;
            existing.UpdatedBy = actorId;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            mutationKind = SecretEventKind.Rotated;
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
            mutationKind = SecretEventKind.Set;
        }

        // Outbox row lands in the same SaveChangesAsync as the
        // EncryptedSecret mutation (ADR 0001 §3 — atomicity). The
        // background OutboxDispatcher picks it up and publishes to
        // NATS; rolling back the secret mutation also rolls back the
        // event.
        _db.AppendSecretChanged(
            definitionKey: dto.DefinitionKey,
            definitionId: definition.Id,
            scopeType: dto.ScopeType,
            scopeId: dto.ScopeId,
            kind: mutationKind);

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

        try
        {
            return _protector.Unprotect(secret.EncryptedValue);
        }
        catch (CryptographicException ex)
        {
            // The stored ciphertext can't be decrypted with the current
            // DataProtection key ring — the master key was rotated or
            // regenerated (historically because keys were ephemeral; see
            // the PersistKeysToFileSystem fix in Program.cs). Treat the
            // secret as ABSENT rather than throwing a 500: a single
            // undecryptable secret must not take down callers. e.g.
            // andy-tasks' PlannerSettingsBootstrapper fetches a planner
            // secret at boot and a 500 here crashed its whole host.
            // Returning null lets the caller degrade; the operator
            // re-sets the secret to repair it (and it now persists).
            _logger.LogWarning(
                ex,
                "[SECRET-UNDECRYPTABLE] Secret for definition '{Definition}' (scope {ScopeType}/{ScopeId}) " +
                "could not be decrypted with the current DataProtection key; treating as absent. " +
                "Re-set the secret to repair it.",
                dto.DefinitionKey, dto.ScopeType, dto.ScopeId);
            return null;
        }
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

        // No rows to delete is a no-op — don't publish a phantom event.
        // Consumers would re-resolve, find no key, and surface a
        // misleading "key was deleted" signal in their UI.
        if (secrets.Count == 0) return;

        _db.EncryptedSecrets.RemoveRange(secrets);

        // rivoli-ai/conductor#925 (M1.2.1). One event per definition.
        // Per-scope events would be noisier without buying anything —
        // consumers invalidate by definition key, not scope. ScopeId
        // is left null in the payload to signal "all scopes".
        _db.AppendSecretChanged(
            definitionKey: definitionKey,
            definitionId: definition.Id,
            scopeType: ScopeType.Machine,
            scopeId: null,
            kind: SecretEventKind.Deleted);

        await _db.SaveChangesAsync(ct);
    }

    private static SecretMetadataDto ToMetadataDto(EncryptedSecret e, string definitionKey) => new(
        e.Id, e.DefinitionId, definitionKey, e.ScopeType, e.ScopeId,
        e.UpdatedBy, e.CreatedAt, e.UpdatedAt);
}
