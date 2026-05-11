// Copyright (c) Rivoli AI 2026. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Andy.Settings.Application.Messaging;
using Andy.Settings.Application.Messaging.Events;
using Andy.Settings.Domain.Entities;
using Andy.Settings.Infrastructure.Data;

namespace Andy.Settings.Infrastructure.Messaging;

// Helper for appending a config.* OutboxEntry to the DbContext in the
// same unit of work as the SettingAssignment change. Mirrors
// andy-issues' StoryEventOutbox / IssueEventOutbox helpers: the caller
// controls SaveChangesAsync, so the outbox row lands in the same
// transaction as the state mutation (ADR 0001 §3).
//
// `causationId` / `parentGeneration` carry the message-id chain when
// the assignment change was driven by an upstream event. User-driven
// writes (REST, CLI, MCP) leave both at the default; the resulting
// outbox row is the root of its causation chain.
public static class ConfigEventOutbox
{
    // Per the AL3 subject scheme: andy.settings.events.config.{app}.{kind}.
    // The application_code lives in the subject so a consumer like
    // mcp-gateway can subscribe to its own slice
    // (`andy.settings.events.config.mcp-gateway.>`) without filtering
    // every message in the stream.
    public static string SubjectFor(string applicationCode, ConfigEventKind kind)
        => $"andy.settings.events.config.{Sanitize(applicationCode)}.{kind.ToSubjectKind()}";

    public static void AppendConfigChanged(
        this SettingsDbContext db,
        SettingAssignment assignment,
        SettingDefinition definition,
        ConfigEventKind kind,
        string? newValueJson,
        Guid? causationId = null,
        int parentGeneration = 0)
    {
        var payload = new ConfigChangedPayload(
            SchemaVersion: 1,
            AssignmentId: assignment.Id,
            Key: definition.Key,
            ApplicationCode: definition.ApplicationCode,
            ScopeType: assignment.ScopeType.ToString(),
            ScopeId: assignment.ScopeId,
            Kind: kind.ToSubjectKind(),
            NewValueDigest: newValueJson is null ? null : Sha256Hex(newValueJson),
            Etag: assignment.Etag,
            Version: assignment.Version,
            UpdatedBy: assignment.UpdatedBy,
            UpdatedAt: assignment.UpdatedAt);

        var subject = SubjectFor(definition.ApplicationCode, kind);

        // Per ADR 0001 §5: an event published in response to a parent
        // message is `parent.generation + 1`; a root event (no parent)
        // is generation 0.
        var generation = causationId is null ? 0 : parentGeneration + 1;

        db.Outbox.Add(new OutboxEntry
        {
            Id = Guid.NewGuid(),
            Subject = subject,
            PayloadType = typeof(ConfigChangedPayload).FullName,
            PayloadJson = JsonSerializer.Serialize(payload, EventJson.Options),
            // Correlate by definition so the audit log can be aligned per-key.
            CorrelationId = definition.Id,
            CausationId = causationId,
            Generation = generation,
            CreatedAt = DateTimeOffset.UtcNow,
        });
    }

    // SHA-256 of the value bytes, lowercase hex. Cheap (~µs per value at
    // realistic config sizes) and gives consumers an opaque tag for
    // re-delivery detection without trusting the publisher's claim that
    // "something changed".
    private static string Sha256Hex(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    // Subject tokens are dot-separated. application_code is a free-form
    // string in the domain model; if a registration ever sneaks dots in
    // it, the subject parse on the consumer side would split mid-token.
    // Replace dots with dashes for the wire — the original
    // application_code lives in the payload for reverse lookup.
    private static string Sanitize(string applicationCode)
        => applicationCode.Replace('.', '-');
}
