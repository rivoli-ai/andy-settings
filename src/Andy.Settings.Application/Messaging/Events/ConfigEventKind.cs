// Copyright (c) Rivoli AI 2026. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Andy.Settings.Application.Messaging.Events;

// Past-tense kind suffix for the andy.settings.events.config.* subject
// taxonomy per ADR 0001 §4 ("Kind is past-tense").
public enum ConfigEventKind
{
    Created,
    Updated,
    Deleted,
}

public static class ConfigEventKindExtensions
{
    public static string ToSubjectKind(this ConfigEventKind kind) => kind switch
    {
        ConfigEventKind.Created => "created",
        ConfigEventKind.Updated => "updated",
        ConfigEventKind.Deleted => "deleted",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
