// Copyright (c) Rivoli AI 2026. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Andy.Settings.Domain.Entities;

// Persistent consumer-side dedup row per ADR 0001 §3 + AK3.
// Consumers MUST dedupe by MsgId using this table — in-memory ring
// buffers don't survive restarts and don't dedupe across replicas.
// A cleanup hosted worker purges rows where ExpiresAt < now.
public class SeenMessage
{
    // The MsgId of the consumed message (matches the publisher's
    // OutboxEntry.Id).
    public Guid MsgId { get; set; }

    // Subject the message arrived on. Kept for diagnostics — the dedup
    // lookup is by MsgId alone.
    public string Subject { get; set; } = string.Empty;

    public DateTimeOffset SeenAt { get; set; } = DateTimeOffset.UtcNow;

    // TTL boundary. After this point the cleanup worker is allowed to
    // remove the row. Default: 90 days for domain events
    // (ANDY_DOMAIN-class subjects); shorter for progress events.
    public DateTimeOffset ExpiresAt { get; set; }
}
