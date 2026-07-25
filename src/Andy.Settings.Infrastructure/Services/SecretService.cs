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
using System.Text.Json;

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

        if (!definition.IsSecret && definition.DataType != SettingDataType.Secret)
            throw new InvalidOperationException($"Definition '{dto.DefinitionKey}' is not a secret-type setting.");

        ValidateScope(definition, dto.ScopeType, dto.ScopeId);

        var encrypted = _protector.Protect(dto.PlaintextValue);

        var existing = await _db.EncryptedSecrets
            .FirstOrDefaultAsync(s =>
                s.DefinitionId == definition.Id &&
                s.ScopeType == dto.ScopeType &&
                s.ScopeKey == (dto.ScopeId ?? string.Empty), ct);

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

    public async Task<string?> GetSecretAsync(GetSecretDto dto, string? actorId = null, CancellationToken ct = default)
    {
        var definition = await _db.SettingDefinitions.FirstOrDefaultAsync(d => d.Key == dto.DefinitionKey, ct)
            ?? throw new KeyNotFoundException($"Definition '{dto.DefinitionKey}' not found.");

        var secret = await _db.EncryptedSecrets
            .FirstOrDefaultAsync(s =>
                s.DefinitionId == definition.Id &&
                s.ScopeType == dto.ScopeType &&
                s.ScopeKey == (dto.ScopeId ?? string.Empty), ct);

        if (secret is null)
            return null;

        try
        {
            var plaintext = _protector.Unprotect(secret.EncryptedValue);

            // Decrypting a secret is the most security-relevant thing this
            // service does and used to leave no trace at all
            // (rivoli-ai/andy-settings#132). BeforeJson/AfterJson stay null —
            // the audit trail records THAT a secret was read, by whom, at which
            // scope; never the value or a digest of it, which would let anyone
            // with audit access confirm a guess.
            await _audit.RecordAsync(new AuditEventDto(
                Guid.NewGuid(), AuditEventType.SecretRead, dto.DefinitionKey,
                dto.ScopeType, dto.ScopeId, "User", actorId,
                null, null, null, DateTimeOffset.UtcNow), ct);

            return plaintext;
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

    /// <summary>
    /// Deletes stored secrets for a definition. Pass <paramref name="scopeType"/>
    /// to delete a single scope, or leave it null to delete every scope.
    /// </summary>
    /// <returns>How many secrets were deleted.</returns>
    /// <remarks>
    /// Deletion used to be all-scopes ONLY, with no way to remove one scope's
    /// secret: clearing a single user's credential wiped the Machine-scope
    /// value and every other user's along with it, while Set and Rotate were
    /// per-scope (rivoli-ai/andy-settings#138). The API layer now requires the
    /// all-scopes form to be requested explicitly.
    /// </remarks>
    public async Task<int> DeleteSecretAsync(
        string definitionKey,
        ScopeType? scopeType = null,
        string? scopeId = null,
        string? actorId = null,
        CancellationToken ct = default)
    {
        var definition = await _db.SettingDefinitions.FirstOrDefaultAsync(d => d.Key == definitionKey, ct)
            ?? throw new KeyNotFoundException($"Definition '{definitionKey}' not found.");

        var query = _db.EncryptedSecrets.Where(s => s.DefinitionId == definition.Id);
        if (scopeType.HasValue)
        {
            var scopeKey = scopeId ?? string.Empty;
            query = query.Where(s => s.ScopeType == scopeType.Value && s.ScopeKey == scopeKey);
        }

        var secrets = await query.ToListAsync(ct);

        // No rows to delete is a no-op — don't publish a phantom event.
        // Consumers would re-resolve, find no key, and surface a
        // misleading "key was deleted" signal in their UI.
        if (secrets.Count == 0) return 0;

        _db.EncryptedSecrets.RemoveRange(secrets);

        // rivoli-ai/conductor#925 (M1.2.1). One event per delete call.
        // Per-scope events would be noisier without buying anything —
        // consumers invalidate by definition key, not scope. A null scopeId
        // on the all-scopes form still signals "all scopes"; a scoped delete
        // now carries the scope it actually removed.
        _db.AppendSecretChanged(
            definitionKey: definitionKey,
            definitionId: definition.Id,
            scopeType: scopeType ?? ScopeType.Machine,
            scopeId: scopeType.HasValue ? scopeId : null,
            kind: SecretEventKind.Deleted);

        await _db.SaveChangesAsync(ct);

        // The delete path published an event but recorded no audit row, so
        // consumers learned about a secret deletion while the audit trail did
        // not (rivoli-ai/andy-settings#132).
        await _audit.RecordAsync(new AuditEventDto(
            Guid.NewGuid(), AuditEventType.SecretDeleted, definitionKey,
            scopeType ?? ScopeType.Machine, scopeType.HasValue ? scopeId : null,
            "User", actorId, null, null, null, DateTimeOffset.UtcNow), ct);

        return secrets.Count;
    }

    private static SecretMetadataDto ToMetadataDto(EncryptedSecret e, string definitionKey) => new(
        e.Id, e.DefinitionId, definitionKey, e.ScopeType, e.ScopeId,
        e.UpdatedBy, e.CreatedAt, e.UpdatedAt);

    private static void ValidateScope(SettingDefinition definition, ScopeType scopeType, string? scopeId)
    {
        if (scopeType == ScopeType.Machine && !string.IsNullOrEmpty(scopeId))
            throw new InvalidOperationException("Machine-scoped secrets must not specify a scopeId.");
        if (scopeType != ScopeType.Machine && string.IsNullOrWhiteSpace(scopeId))
            throw new InvalidOperationException($"{scopeType}-scoped secrets require a scopeId.");
        if (string.IsNullOrWhiteSpace(definition.AllowedScopesJson))
            return;

        try
        {
            var allowed = JsonSerializer.Deserialize<string[]>(definition.AllowedScopesJson) ?? [];
            if (!allowed.Any(value => Enum.TryParse<ScopeType>(value, true, out var parsed) && parsed == scopeType))
                throw new InvalidOperationException(
                    $"Scope '{scopeType}' is not allowed for definition '{definition.Key}'.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Definition '{definition.Key}' has invalid allowed-scopes metadata.", ex);
        }
    }
}
