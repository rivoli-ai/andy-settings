// Copyright (c) Rivoli AI 2026. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Andy.Settings.Client;

public sealed class AndySettingsOptions
{
    public const string SectionName = "AndySettings";

    public string? ApiBaseUrl { get; set; }
    public string ApplicationCode { get; set; } = "andy-agents";
    public string? Environment { get; set; }
    public string? BearerToken { get; set; }
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
    public Dictionary<string, string?> Defaults { get; set; } = new();
    public List<string> TrackedKeys { get; set; } = new();
    public int RefreshIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Dev-only escape hatch — when true the HttpClient for andy-settings accepts
    /// any TLS certificate. Intended for local docker-compose where andy-settings
    /// serves a self-signed cert bound to a different host name.
    /// </summary>
    public bool SkipCertificateValidation { get; set; }
}
