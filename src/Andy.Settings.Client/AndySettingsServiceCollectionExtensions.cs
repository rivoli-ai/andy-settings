// Copyright (c) Rivoli AI 2026. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Andy.Auth.M2MClient;
using Andy.Settings.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Andy.Settings.Client;

public static class AndySettingsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the andy-settings client with the legacy static
    /// <see cref="IAndySettingsTokenProvider"/> — reads
    /// <c>AndySettings:BearerToken</c> from config once at startup and
    /// never refreshes. Suitable for short-lived processes and tests.
    /// Long-lived services should call
    /// <see cref="AddAndySettingsClientWithM2M(IServiceCollection, IConfiguration)"/>
    /// instead so the token auto-renews from andy-auth via
    /// <c>client_credentials</c>.
    /// </summary>
    public static IServiceCollection AddAndySettingsClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        BindOptionsAndCore(services, configuration);
        services.TryAddSingleton<IAndySettingsTokenProvider, StaticAndySettingsTokenProvider>();
        RegisterHttpClients(services, useBearerHandler: false);
        services.AddHostedService<SettingsRefreshService>();
        return services;
    }

    /// <summary>
    /// Registers the andy-settings client AND
    /// <c>Andy.Auth.M2MClient</c> infrastructure, then wires the
    /// outbound HttpClients through
    /// <see cref="ServiceBearerHandler"/> so every request to
    /// andy-settings carries an M2M-acquired bearer. The host's
    /// <c>AndyAuth</c> configuration section must define
    /// <c>ClientId</c>, <c>ClientSecretEnvVar</c>, and either
    /// <c>TokenEndpoint</c> or <c>Authority</c> (conductor#990).
    /// Idempotent — calling either overload after the other is safe;
    /// the M2M-backed provider wins because <c>Add</c> replaces the
    /// <c>TryAdd*</c> registration.
    /// </summary>
    public static IServiceCollection AddAndySettingsClientWithM2M(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAndyAuthM2M(configuration);
        BindOptionsAndCore(services, configuration);
        // Replace any earlier StaticAndySettingsTokenProvider registration
        // — Singleton + Add overwrites whatever TryAddSingleton put there.
        services.AddSingleton<IAndySettingsTokenProvider, ClientCredentialsAndySettingsTokenProvider>();
        RegisterHttpClients(services, useBearerHandler: true);
        services.AddHostedService<SettingsRefreshService>();
        return services;
    }

    private static void BindOptionsAndCore(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<AndySettingsOptions>()
            .Bind(configuration.GetSection(AndySettingsOptions.SectionName));

        services.TryAddSingleton<SettingsSnapshot>();
        services.TryAddSingleton<ISettingsSnapshot>(sp => sp.GetRequiredService<SettingsSnapshot>());
    }

    private static void RegisterHttpClients(IServiceCollection services, bool useBearerHandler)
    {
        var clientBuilder = services
            .AddHttpClient<IAndySettingsClient, AndySettingsHttpClient>(ConfigureClient)
            .ConfigurePrimaryHttpMessageHandler(BuildPrimaryHandler);

        var adminBuilder = services
            .AddHttpClient<ISettingsAdminClient, SettingsAdminHttpClient>(ConfigureClient)
            .ConfigurePrimaryHttpMessageHandler(BuildPrimaryHandler);

        if (useBearerHandler)
        {
            clientBuilder.AddBearerFromAndyAuthM2M();
            adminBuilder.AddBearerFromAndyAuthM2M();
        }
    }

    private static void ConfigureClient(IServiceProvider sp, HttpClient client)
    {
        var options = sp.GetRequiredService<IOptions<AndySettingsOptions>>().Value;
        if (!string.IsNullOrWhiteSpace(options.ApiBaseUrl))
            client.BaseAddress = new Uri(options.ApiBaseUrl);
        if (options.Timeout > TimeSpan.Zero)
            client.Timeout = options.Timeout;
    }

    private static HttpMessageHandler BuildPrimaryHandler(IServiceProvider sp)
    {
        var options = sp.GetRequiredService<IOptions<AndySettingsOptions>>().Value;
        var handler = new HttpClientHandler();
        if (options.SkipCertificateValidation)
        {
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }
        return handler;
    }
}
