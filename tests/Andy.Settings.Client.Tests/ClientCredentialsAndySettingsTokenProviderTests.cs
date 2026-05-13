// Copyright (c) Rivoli AI 2026. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Andy.Auth.M2MClient;
using Andy.Settings.Client;
using FluentAssertions;

namespace Andy.Settings.Client.Tests;

public sealed class ClientCredentialsAndySettingsTokenProviderTests
{
    [Fact]
    public async Task GetTokenAsync_DelegatesToServiceTokenProvider()
    {
        var underlying = new FakeServiceTokenProvider("m2m-token-xyz");
        var adapter = new ClientCredentialsAndySettingsTokenProvider(underlying);

        var token = await adapter.GetTokenAsync(CancellationToken.None);

        token.Should().Be("m2m-token-xyz");
        underlying.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task GetTokenAsync_PropagatesCancellation()
    {
        var underlying = new FakeServiceTokenProvider("ok");
        var adapter = new ClientCredentialsAndySettingsTokenProvider(underlying);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await adapter.GetTokenAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GetTokenAsync_ReturnsNull_WhenUnderlyingReturnsNull()
    {
        // ClientCredentialsTokenProvider never returns null in practice
        // (it throws ServiceTokenException), but the IAndySettingsTokenProvider
        // contract allows null, so the adapter should pass it through.
        var underlying = new FakeServiceTokenProvider(null!);
        var adapter = new ClientCredentialsAndySettingsTokenProvider(underlying);

        var token = await adapter.GetTokenAsync(CancellationToken.None);

        token.Should().BeNull();
    }

    private sealed class FakeServiceTokenProvider : IServiceTokenProvider
    {
        private readonly string? _token;
        public int CallCount { get; private set; }

        public FakeServiceTokenProvider(string? token) { _token = token; }

        public Task<string> GetTokenAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(_token!);
        }
    }
}
