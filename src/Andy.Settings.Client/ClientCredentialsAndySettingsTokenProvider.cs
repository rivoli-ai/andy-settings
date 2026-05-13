// Copyright (c) Rivoli AI 2026. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Andy.Auth.M2MClient;

namespace Andy.Settings.Client;

/// <summary>
/// <see cref="IAndySettingsTokenProvider"/> adapter that delegates to the
/// shared <see cref="IServiceTokenProvider"/> from
/// <c>Andy.Auth.M2MClient</c>. Registered automatically by
/// <c>AddAndySettingsClient</c> when the host service has called
/// <c>AddAndyAuthM2M</c> first; otherwise the legacy
/// <see cref="StaticAndySettingsTokenProvider"/> remains in place.
/// </summary>
internal sealed class ClientCredentialsAndySettingsTokenProvider : IAndySettingsTokenProvider
{
    private readonly IServiceTokenProvider _tokens;

    public ClientCredentialsAndySettingsTokenProvider(IServiceTokenProvider tokens)
    {
        _tokens = tokens;
    }

    public async Task<string?> GetTokenAsync(CancellationToken ct = default)
        => await _tokens.GetTokenAsync(ct).ConfigureAwait(false);
}
