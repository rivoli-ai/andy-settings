// Copyright (c) Rivoli AI 2026. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Andy.Settings.Application.Messaging.Events;

// rivoli-ai/conductor#925 (M1.2.1). Past-tense kind suffix for the
// `andy.settings.events.definition.<key>.<kind>` subject taxonomy a
// secret mutation produces (ADR 0001 §4: kinds are past-tense).
//
// The downstream consumer (`andy-models`'s `SettingsChangeConsumer`)
// filters on `andy.settings.events.definition.>` and only acts on
// the `.updated` suffix today. We emit `.updated` for both Set and
// Rotate, and `.deleted` for Delete, so:
//
//   - Set / Rotate → cache invalidation fires next time the key is
//     requested (the consumer re-reads via the resolver).
//   - Delete → the consumer treats it as "no key" and surfaces a
//     "missing key" preflight error in launch UIs.
public enum SecretEventKind
{
    Set,
    Rotated,
    Deleted,
}

public static class SecretEventKindExtensions
{
    // The subject suffix actually emitted on the wire. Set + Rotated
    // both collapse to `updated` to match the existing consumer's
    // filter; the distinct kind survives in the payload's `Mutation`
    // field for consumers that care.
    public static string ToSubjectKind(this SecretEventKind kind) => kind switch
    {
        SecretEventKind.Set => "updated",
        SecretEventKind.Rotated => "updated",
        SecretEventKind.Deleted => "deleted",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    // The full mutation tag carried in the payload. Distinguishes
    // Set (first-time write) from Rotated (overwrite) for consumers
    // that want to log them differently. Matches the spec at
    // rivoli-ai/conductor#925.
    public static string ToMutationTag(this SecretEventKind kind) => kind switch
    {
        SecretEventKind.Set => "set",
        SecretEventKind.Rotated => "rotated",
        SecretEventKind.Deleted => "revoked",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
