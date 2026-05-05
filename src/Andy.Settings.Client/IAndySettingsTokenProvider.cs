// Copyright (c) Rivoli AI 2026. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Andy.Settings.Client;

public interface IAndySettingsTokenProvider
{
    Task<string?> GetTokenAsync(CancellationToken ct = default);
}
