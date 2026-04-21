// Copyright (c) Rivoli AI 2026. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Andy.Settings.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Andy.Settings.Client;

internal sealed class SettingsRefreshService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SettingsSnapshot _snapshot;
    private readonly IOptionsMonitor<AndySettingsOptions> _options;
    private readonly ILogger<SettingsRefreshService> _logger;

    public SettingsRefreshService(
        IServiceScopeFactory scopeFactory,
        SettingsSnapshot snapshot,
        IOptionsMonitor<AndySettingsOptions> options,
        ILogger<SettingsRefreshService> logger)
    {
        _scopeFactory = scopeFactory;
        _snapshot = snapshot;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RefreshAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var interval = TimeSpan.FromSeconds(Math.Max(5, _options.CurrentValue.RefreshIntervalSeconds));
            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            await RefreshAsync(stoppingToken);
        }
    }

    internal async Task RefreshAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IAndySettingsClient>();
        await RefreshAsync(client, ct);
    }

    internal async Task RefreshAsync(IAndySettingsClient client, CancellationToken ct)
    {
        var keys = _options.CurrentValue.TrackedKeys;
        if (keys.Count == 0)
            return;

        try
        {
            var values = await client.GetBatchAsync(keys, ct: ct);
            _snapshot.Update(values);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh andy-settings snapshot");
        }
    }
}
