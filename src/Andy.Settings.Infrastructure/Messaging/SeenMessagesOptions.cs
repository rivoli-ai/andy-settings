// Copyright (c) Rivoli AI 2026. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Andy.Settings.Infrastructure.Messaging;

// Knobs for the seen-messages cleanup background job. Bound from the
// "Messaging:SeenMessages" configuration section.
public sealed class SeenMessagesOptions
{
    public const string SectionName = "Messaging:SeenMessages";

    // How often the cleanup job wakes to purge expired rows. Cleanup is
    // background work — no need to run it on the same cadence as the
    // outbox dispatcher.
    public TimeSpan PurgeInterval { get; set; } = TimeSpan.FromMinutes(15);

    // Max rows to delete per purge tick. Caps the transaction size.
    public int PurgeBatchSize { get; set; } = 1000;

    // TTL applied to newly-recorded seen rows. Defaults to 90 days,
    // matching the ANDY_DOMAIN stream retention class per AK5. Override
    // per-consumer if a subscription is on a shorter-retention stream.
    public TimeSpan DefaultTtl { get; set; } = TimeSpan.FromDays(90);
}
