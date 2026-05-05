// Copyright (c) Rivoli AI 2026. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Andy.Settings.Client;

public sealed record SettingsResolutionContext
{
    public string? UserId { get; init; }
    public string? TeamId { get; init; }
    public string? WorkspaceId { get; init; }
    public string? ApplicationCode { get; init; }
    public string? ServiceCode { get; init; }
}
