// Copyright (c) Rivoli AI 2026. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Andy.Auth.M2MClient;
using Andy.Settings.Client;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Andy.Settings.Client.Tests;

public sealed class AndySettingsServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAndySettingsClient_RegistersStaticTokenProvider()
    {
        var config = NewConfig(new()
        {
            ["AndySettings:ApiBaseUrl"] = "https://settings.test",
            ["AndySettings:BearerToken"] = "static-bearer",
        });
        var services = new ServiceCollection().AddLogging();

        services.AddAndySettingsClient(config);
        using var sp = services.BuildServiceProvider();

        var provider = sp.GetRequiredService<IAndySettingsTokenProvider>();
        provider.Should().BeOfType<StaticAndySettingsTokenProvider>();
    }

    [Fact]
    public async Task AddAndySettingsClient_StaticProviderReturnsBearerFromConfig()
    {
        var config = NewConfig(new()
        {
            ["AndySettings:ApiBaseUrl"] = "https://settings.test",
            ["AndySettings:BearerToken"] = "config-bearer-abc",
        });
        var services = new ServiceCollection().AddLogging().AddAndySettingsClient(config);
        using var sp = services.BuildServiceProvider();

        var token = await sp.GetRequiredService<IAndySettingsTokenProvider>().GetTokenAsync();

        token.Should().Be("config-bearer-abc");
    }

    [Fact]
    public void AddAndySettingsClientWithM2M_RegistersClientCredentialsProvider()
    {
        var config = NewConfig(new()
        {
            ["AndySettings:ApiBaseUrl"] = "https://settings.test",
            ["AndyAuth:Authority"] = "https://andy-auth.test",
            ["AndyAuth:ClientId"] = "andy-code-index-api",
            ["AndyAuth:ClientSecretEnvVar"] = "ANDY_CODE_INDEX_API_SECRET",
            ["AndyAuth:Scope"] = "scp:urn:andy-settings-api",
        });
        var services = new ServiceCollection().AddLogging();

        services.AddAndySettingsClientWithM2M(config);
        using var sp = services.BuildServiceProvider();

        var provider = sp.GetRequiredService<IAndySettingsTokenProvider>();
        provider.Should().BeOfType<ClientCredentialsAndySettingsTokenProvider>();

        // The underlying M2M provider must also be in the container so
        // ServiceBearerHandler can grab it for outbound requests.
        sp.GetService<IServiceTokenProvider>().Should().NotBeNull();
        sp.GetService<IRefreshableServiceTokenProvider>().Should().NotBeNull();
    }

    [Fact]
    public void AddAndySettingsClientWithM2M_OverridesStaticProvider_IfBothCalled()
    {
        // Some hosts call both extensions (e.g. tests + production code
        // sharing setup helpers). The M2M-backed adapter must win
        // regardless of call order, because the static one silently
        // breaks once its bearer expires.
        var config = NewConfig(new()
        {
            ["AndySettings:ApiBaseUrl"] = "https://settings.test",
            ["AndySettings:BearerToken"] = "should-be-ignored",
            ["AndyAuth:Authority"] = "https://andy-auth.test",
            ["AndyAuth:ClientId"] = "andy-code-index-api",
            ["AndyAuth:ClientSecretEnvVar"] = "ANDY_CODE_INDEX_API_SECRET",
        });
        var services = new ServiceCollection().AddLogging();
        services.AddAndySettingsClient(config);
        services.AddAndySettingsClientWithM2M(config);
        using var sp = services.BuildServiceProvider();

        sp.GetRequiredService<IAndySettingsTokenProvider>()
            .Should().BeOfType<ClientCredentialsAndySettingsTokenProvider>();
    }

    [Fact]
    public void AddAndySettingsClientWithM2M_IsIdempotent()
    {
        var config = NewConfig(new()
        {
            ["AndyAuth:Authority"] = "https://andy-auth.test",
            ["AndyAuth:ClientId"] = "andy-code-index-api",
            ["AndyAuth:ClientSecretEnvVar"] = "ANDY_CODE_INDEX_API_SECRET",
        });
        var services = new ServiceCollection().AddLogging();
        services.AddAndySettingsClientWithM2M(config);
        services.AddAndySettingsClientWithM2M(config);
        services.AddAndySettingsClientWithM2M(config);

        // Building the provider must succeed, and the M2M-backed
        // provider must be the registered IAndySettingsTokenProvider.
        using var sp = services.BuildServiceProvider();
        sp.GetRequiredService<IAndySettingsTokenProvider>()
            .Should().BeOfType<ClientCredentialsAndySettingsTokenProvider>();
    }

    private static IConfigurationRoot NewConfig(Dictionary<string, string?> values)
        => new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
