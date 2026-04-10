using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Andy.Settings.Infrastructure.Telemetry;

/// <summary>
/// OpenTelemetry instrumentation for Andy Settings.
/// </summary>
public static class SettingsTelemetry
{
    public const string ServiceName = "Andy.Settings";

    public static readonly ActivitySource ActivitySource = new(ServiceName);
    public static readonly Meter Meter = new(ServiceName);

    // Counters
    public static readonly Counter<long> DefinitionsCreated =
        Meter.CreateCounter<long>("settings.definitions.created", "definitions", "Number of definitions created");

    public static readonly Counter<long> ValuesSet =
        Meter.CreateCounter<long>("settings.values.set", "values", "Number of values set or updated");

    public static readonly Counter<long> ValuesDeleted =
        Meter.CreateCounter<long>("settings.values.deleted", "values", "Number of values deleted");

    public static readonly Counter<long> SecretsRotated =
        Meter.CreateCounter<long>("settings.secrets.rotated", "secrets", "Number of secrets set or rotated");

    public static readonly Counter<long> Resolutions =
        Meter.CreateCounter<long>("settings.resolutions", "resolutions", "Number of effective value resolutions");

    public static readonly Counter<long> RbacChecks =
        Meter.CreateCounter<long>("settings.rbac.checks", "checks", "Number of RBAC permission checks");

    public static readonly Counter<long> RbacDenials =
        Meter.CreateCounter<long>("settings.rbac.denials", "denials", "Number of RBAC permission denials");

    // Histograms
    public static readonly Histogram<double> ResolutionDuration =
        Meter.CreateHistogram<double>("settings.resolution.duration", "s", "Duration of effective value resolution");
}
