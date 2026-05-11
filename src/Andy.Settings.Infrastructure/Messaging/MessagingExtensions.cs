// Copyright (c) Rivoli AI 2026. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Andy.Settings.Application.Messaging;
using Andy.Settings.Infrastructure.Messaging.Nats;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Andy.Settings.Infrastructure.Messaging;

public static class MessagingExtensions
{
    // Register the messaging stack: IMessageBus (Nats when
    // Messaging:Provider=Nats, otherwise InMemory), OutboxDispatcher,
    // and the SeenMessages cleanup job.
    //
    // The AK1 fail-loud guard lives in Program.cs (next to the
    // configuration read) so it can fire before any DI is built — by
    // the time this extension runs the provider has already been
    // validated for the current environment.
    public static IServiceCollection AddSettingsMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment env)
    {
        var provider = configuration["Messaging:Provider"] ?? "InMemory";

        if (string.Equals(provider, "Nats", StringComparison.OrdinalIgnoreCase))
        {
            services.Configure<NatsOptions>(
                configuration.GetSection(NatsOptions.SectionName));
            services.AddSingleton<NatsMessageBus>();
            services.AddSingleton<IMessageBus>(sp => sp.GetRequiredService<NatsMessageBus>());
            services.AddHostedService<NatsStreamProvisioner>();
        }
        else
        {
            services.AddSingleton<IMessageBus, InMemoryMessageBus>();
        }

        services.Configure<OutboxDispatcherOptions>(
            configuration.GetSection(OutboxDispatcherOptions.SectionName));
        services.AddHostedService<OutboxDispatcher>();

        services.Configure<SeenMessagesOptions>(
            configuration.GetSection(SeenMessagesOptions.SectionName));
        services.AddScoped<SqlSeenMessageStore>();
        services.AddHostedService<SeenMessagesCleanupJob>();

        return services;
    }
}
