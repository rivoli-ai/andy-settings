// Copyright (c) Rivoli AI 2026. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Andy.Settings.Application.Messaging.Events;

// rivoli-ai/conductor#925 (M1.2.1). Wire payload for the
// `andy.settings.events.definition.<key>.<kind>` subject family
// produced by a secret mutation in SecretService.
//
// Per ADR 0001 §4 the payload is a notification — the value itself is
// NEVER on the wire. Consumers re-resolve via the API
// (`GET /api/secrets/<definitionKey>`) and authenticate with their
// own bearer; that flow is what lands in the audit trail. The
// payload's sole job is to tell consumers "this definition just
// changed; invalidate your cache".
//
// Schema-version 1 is the initial release. Add fields with null-safe
// defaults; bump the integer on a breaking change.
public sealed record SecretChangedPayload(
    int SchemaVersion,
    string DefinitionKey,
    string ScopeType,
    string? ScopeId,
    string Mutation,
    DateTimeOffset TimestampUtc);
