// Copyright (c) Rivoli AI 2026. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Andy.Settings.Application.Messaging.Events;

// Wire payload for `andy.settings.events.config.<app>.<kind>` events.
//
// Per ADR 0001 §4 the payload is intentionally a notification, not the
// authoritative value — consumers GET the resolved value over REST so a
// tamper-resistant audit trail flows through the API. NewValueDigest is
// a SHA-256 hex of the new ValueJson; it lets consumers detect
// re-delivery vs. a real change without fetching the value.
//
// Schema-version 1 is the initial release. Add fields with null-safe
// defaults; bump the integer on a breaking change.
public sealed record ConfigChangedPayload(
    int SchemaVersion,
    Guid AssignmentId,
    string Key,
    string ApplicationCode,
    string ScopeType,
    string? ScopeId,
    string Kind,
    string? NewValueDigest,
    string? Etag,
    int Version,
    string? UpdatedBy,
    DateTimeOffset UpdatedAt);
