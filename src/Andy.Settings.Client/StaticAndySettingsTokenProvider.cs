// Copyright (c) Rivoli AI 2026. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Andy.Settings.Client;
using Microsoft.Extensions.Options;

namespace Andy.Settings.Client;

internal sealed class StaticAndySettingsTokenProvider : IAndySettingsTokenProvider
{
    private readonly IOptionsMonitor<AndySettingsOptions> _options;

    public StaticAndySettingsTokenProvider(IOptionsMonitor<AndySettingsOptions> options)
    {
        _options = options;
    }

    public Task<string?> GetTokenAsync(CancellationToken ct = default)
        => Task.FromResult(_options.CurrentValue.BearerToken);
}
