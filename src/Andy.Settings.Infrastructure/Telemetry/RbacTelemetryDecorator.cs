using System.Diagnostics;

namespace Andy.Settings.Infrastructure.Telemetry;

/// <summary>
/// Provides OpenTelemetry instrumentation for RBAC permission checks.
/// Records metrics (counters for checks and denials) and creates activity spans
/// with detailed tags for observability.
/// </summary>
public static class RbacTelemetryExtensions
{
    /// <summary>
    /// Records an RBAC permission check with OpenTelemetry metrics and tracing.
    /// Increments the rbac.checks counter and, if denied, the rbac.denials counter.
    /// Creates an activity span with permission, user, scope, and result tags.
    /// </summary>
    /// <param name="permission">The permission that was checked (e.g., "definition:read").</param>
    /// <param name="userId">The user ID for which the check was performed, or null.</param>
    /// <param name="scopeType">The scope type context of the check (e.g., "Application"), or null.</param>
    /// <param name="scopeId">The scope ID context of the check, or null.</param>
    /// <param name="allowed">Whether the permission check was allowed.</param>
    public static void RecordRbacCheck(
        string permission,
        string? userId,
        string? scopeType,
        string? scopeId,
        bool allowed)
    {
        var resultTag = allowed ? "allowed" : "denied";

        // Increment the RBAC checks counter
        SettingsTelemetry.RbacChecks.Add(1,
            new KeyValuePair<string, object?>("permission", permission),
            new KeyValuePair<string, object?>("result", resultTag));

        if (!allowed)
        {
            // Increment the RBAC denials counter for denied checks
            SettingsTelemetry.RbacDenials.Add(1,
                new KeyValuePair<string, object?>("permission", permission));
        }

        // Create an activity span for distributed tracing
        using var activity = SettingsTelemetry.ActivitySource.StartActivity("rbac.check");
        activity?.SetTag("rbac.permission", permission);
        activity?.SetTag("rbac.user_id", userId);
        activity?.SetTag("rbac.scope_type", scopeType);
        activity?.SetTag("rbac.scope_id", scopeId);
        activity?.SetTag("rbac.result", resultTag);
    }
}
