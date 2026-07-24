// Copyright (c) Rivoli AI 2026. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Andy.Settings.Infrastructure.Messaging.Nats;

public sealed class NatsOptions
{
    public const string SectionName = "Messaging:Nats";

    public string Url { get; set; } = "nats://localhost:4222";

    // Config events are domain events per ADR 0001 §7 resolved-decision-1,
    // so they live on ANDY_DOMAIN (90-day retention), which is SHARED with
    // andy-tasks, andy-issues, andy-agents and andy-rbac. NatsStreamProvisioner
    // only ever adds subjects to it — see the note there.
    public string StreamName { get; set; } = "ANDY_DOMAIN";

    /// <summary>
    /// Event subjects this service owns on the stream. Falls back to
    /// <see cref="DefaultStreamSubjects"/> when configuration supplies none.
    /// </summary>
    /// <remarks>
    /// Deliberately EMPTY rather than pre-populated. The .NET configuration
    /// binder appends bound array elements to whatever the property already
    /// holds instead of replacing them, so a non-empty default silently
    /// duplicates any value that appsettings.json also supplies — and NATS
    /// rejects a stream config with duplicate subjects, taking the host down on
    /// boot (rivoli-ai/andy-settings#149). Keeping this empty means the bound
    /// value is exactly what configuration asked for.
    /// </remarks>
    public string[] StreamSubjects { get; set; } = [];

    /// <summary>Used when <see cref="StreamSubjects"/> is not configured.</summary>
    public static readonly string[] DefaultStreamSubjects = ["andy.settings.events.>"];

    public TimeSpan MaxAge { get; set; } = TimeSpan.FromDays(90);

    // DLQ subject prefix per ADR 0001 §resolved-decisions-3.
    public string DlqPrefix { get; set; } = "andy.settings.dlq";
}
