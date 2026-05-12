// Copyright (c) Rivoli AI 2026. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;
using Andy.Settings.Application.Messaging;
using Andy.Settings.Application.Messaging.Events;
using Andy.Settings.Domain.Entities;
using Andy.Settings.Domain.Enums;
using Andy.Settings.Infrastructure.Data;

namespace Andy.Settings.Infrastructure.Messaging;

// rivoli-ai/conductor#925 (M1.2.1). Helper for appending a
// secret-mutation OutboxEntry to the DbContext in the same unit of
// work as the EncryptedSecret change. Mirrors ConfigEventOutbox: the
// caller controls SaveChangesAsync, so the outbox row lands in the
// same transaction as the state mutation (ADR 0001 §3 — atomicity).
//
// Subject taxonomy
// ----------------
// `andy.settings.events.definition.<definitionKey>.<kind>` — matches
// the wildcard `andy.settings.events.definition.>` that
// andy-models' `SettingsChangeConsumer` already subscribes to.
// Set + Rotated both emit `.updated`; Delete emits `.deleted`.
//
// Payload
// -------
// Notification only. **Never** the value. Consumers re-resolve via
// the API and authenticate with their own bearer; that's the audit
// path. See `SecretChangedPayload`.
public static class SecretEventOutbox
{
    // The wildcard the andy-models consumer subscribes to (drift
    // here silently breaks the live-rotation cache invalidation).
    public const string DefinitionEventSubjectPrefix = "andy.settings.events.definition.";

    public static string SubjectFor(string definitionKey, SecretEventKind kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionKey);
        // The definitionKey may itself contain dots (e.g.
        // `andy.models.providers.anthropic.apiKey`). NATS treats dots
        // as token separators in subjects; the wildcard the consumer
        // uses (`>` greedy) handles arbitrary depth, so we pass the
        // key through verbatim.
        return $"{DefinitionEventSubjectPrefix}{definitionKey}.{kind.ToSubjectKind()}";
    }

    public static void AppendSecretChanged(
        this SettingsDbContext db,
        string definitionKey,
        Guid definitionId,
        ScopeType scopeType,
        string? scopeId,
        SecretEventKind kind,
        Guid? causationId = null,
        int parentGeneration = 0)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionKey);

        var payload = new SecretChangedPayload(
            SchemaVersion: 1,
            DefinitionKey: definitionKey,
            ScopeType: scopeType.ToString(),
            ScopeId: scopeId,
            Mutation: kind.ToMutationTag(),
            TimestampUtc: DateTimeOffset.UtcNow);

        // Per ADR 0001 §5: a child event of an upstream message is
        // `parent.generation + 1`; root events (no parent) are 0.
        var generation = causationId is null ? 0 : parentGeneration + 1;

        db.Outbox.Add(new OutboxEntry
        {
            Id = Guid.NewGuid(),
            Subject = SubjectFor(definitionKey, kind),
            PayloadType = typeof(SecretChangedPayload).FullName,
            PayloadJson = JsonSerializer.Serialize(payload, EventJson.Options),
            // Correlate by definition so the audit trail can be
            // aligned per-key.
            CorrelationId = definitionId,
            CausationId = causationId,
            Generation = generation,
            CreatedAt = DateTimeOffset.UtcNow,
        });
    }
}
