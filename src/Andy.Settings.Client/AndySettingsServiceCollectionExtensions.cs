// Copyright (c) Rivoli AI 2026. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Andy.Settings.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Andy.Settings.Client;

public static class AndySettingsServiceCollectionExtensions
{
    public static IServiceCollection AddAndySettingsClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<AndySettingsOptions>()
            .Bind(configuration.GetSection(AndySettingsOptions.SectionName));

        services.TryAddSingleton<IAndySettingsTokenProvider, StaticAndySettingsTokenProvider>();

        services.TryAddSingleton<SettingsSnapshot>();
        services.TryAddSingleton<ISettingsSnapshot>(sp => sp.GetRequiredService<SettingsSnapshot>());

        services
            .AddHttpClient<IAndySettingsClient, AndySettingsHttpClient>((sp, client) => ConfigureClient(sp, client))
            .ConfigurePrimaryHttpMessageHandler(sp => BuildPrimaryHandler(sp));

        services
            .AddHttpClient<ISettingsAdminClient, SettingsAdminHttpClient>((sp, client) => ConfigureClient(sp, client))
            .ConfigurePrimaryHttpMessageHandler(sp => BuildPrimaryHandler(sp));

        services.AddHostedService<SettingsRefreshService>();

        return services;
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
