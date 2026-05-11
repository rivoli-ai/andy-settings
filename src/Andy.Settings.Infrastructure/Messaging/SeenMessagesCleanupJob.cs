// Copyright (c) Rivoli AI 2026. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Andy.Settings.Infrastructure.Messaging;

// Background worker that purges expired SeenMessage rows. Runs on its
// own cadence (Messaging:SeenMessages:PurgeInterval) — not coupled to
// the outbox dispatcher. Errors are logged and the loop continues.
public sealed class SeenMessagesCleanupJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SeenMessagesCleanupJob> _logger;
    private readonly SeenMessagesOptions _options;

    public SeenMessagesCleanupJob(
        IServiceScopeFactory scopeFactory,
        ILogger<SeenMessagesCleanupJob> logger,
        IOptions<SeenMessagesOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "SeenMessagesCleanupJob started (interval={Interval}, batch={Batch})",
            _options.PurgeInterval, _options.PurgeBatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var store = scope.ServiceProvider.GetRequiredService<SqlSeenMessageStore>();
                var purged = await store.PurgeExpiredAsync(_options.PurgeBatchSize, stoppingToken);
                if (purged > 0)
                {
                    _logger.LogDebug("Purged {Count} expired seen-message rows", purged);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SeenMessagesCleanupJob tick failed; will retry");
            }

            try
            {
                await Task.Delay(_options.PurgeInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
