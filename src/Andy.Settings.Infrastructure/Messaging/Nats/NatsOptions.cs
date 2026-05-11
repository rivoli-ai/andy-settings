// Copyright (c) Rivoli AI 2026. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Andy.Settings.Infrastructure.Messaging.Nats;

public sealed class NatsOptions
{
    public const string SectionName = "Messaging:Nats";

    public string Url { get; set; } = "nats://localhost:4222";

    // Config events are domain events per ADR 0001 §7 resolved-decision-1,
    // so they live on ANDY_DOMAIN (90-day retention). The provisioner is
    // idempotent — it's safe for andy-settings to call CreateOrUpdate on a
    // stream already provisioned by andy-tasks or andy-issues.
    public string StreamName { get; set; } = "ANDY_DOMAIN";
    public string[] StreamSubjects { get; set; } = ["andy.settings.events.>"];
    public TimeSpan MaxAge { get; set; } = TimeSpan.FromDays(90);

    // DLQ subject prefix per ADR 0001 §resolved-decisions-3.
    public string DlqPrefix { get; set; } = "andy.settings.dlq";
}
