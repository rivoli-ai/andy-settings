// Copyright (c) Rivoli AI 2026. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Andy.Settings.Client;

public interface ISettingsSnapshot
{
    string? GetString(string key);
    int? GetInt(string key);
    bool? GetBool(string key);
    IReadOnlyCollection<string> Keys { get; }
    DateTimeOffset? LastRefreshedAt { get; }
}
