// Copyright (c) Rivoli AI 2026. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using Andy.Settings.Infrastructure.Telemetry;
using Andy.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Andy.Settings.Tests.Integration.Telemetry;

/// <summary>
/// OT5 (rivoli-ai/conductor#1263). Asserts the andy-settings service:
///   1. Registers <see cref="SettingsTelemetry.ActivitySource"/> +
///      <see cref="SettingsTelemetry.Meter"/> with the shared library.
///   2. Surfaces spans started on the domain source.
/// </summary>
public class AndyTelemetryAdoptionTests
{
    [Fact]
    public void AddAndyTelemetry_registers_settings_source_and_meter()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AndyTelemetry:ServiceName"] = "andy-settings",
            })
            .Build();

        services.AddAndyTelemetry(configuration, o =>
        {
            o.ActivitySources.Add(SettingsTelemetry.ServiceName);
            o.Meters.Add(SettingsTelemetry.ServiceName);
            o.EnableAspNetCoreInstrumentation = false;
            o.EnableHttpClientInstrumentation = true;
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<AndyTelemetryOptions>();

        Assert.Contains(SettingsTelemetry.ServiceName, options.ActivitySources);
        Assert.Contains(SettingsTelemetry.ServiceName, options.Meters);
    }

    [Fact]
    public void SettingsActivitySource_emits_when_listened_to()
    {
        var captured = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == SettingsTelemetry.ServiceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => captured.Add(activity),
        };
        ActivitySource.AddActivityListener(listener);

        using (var activity = SettingsTelemetry.ActivitySource.StartActivity("ResolveValue"))
        {
            Assert.NotNull(activity);
            activity!.SetTag("setting.key", "FeatureX:Enabled");
        }

        Assert.Single(captured);
        Assert.Equal("ResolveValue", captured[0].OperationName);
        Assert.Equal("FeatureX:Enabled", captured[0].GetTagItem("setting.key"));
    }

    [Fact]
    public void ResolutionsCounter_isOnTheCanonicalMeter()
    {
        Assert.Equal("Andy.Settings", SettingsTelemetry.Resolutions.Meter.Name);
        Assert.Equal("settings.resolutions", SettingsTelemetry.Resolutions.Name);
    }
}
