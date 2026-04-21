// Copyright (c) Rivoli AI 2026. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Andy.Settings.Client;

public interface IAndySettingsClient
{
    Task<string?> GetStringAsync(
        string key,
        SettingsResolutionContext? context = null,
        CancellationToken ct = default);

    Task<int?> GetIntAsync(
        string key,
        SettingsResolutionContext? context = null,
        CancellationToken ct = default);

    Task<bool?> GetBoolAsync(
        string key,
        SettingsResolutionContext? context = null,
        CancellationToken ct = default);

    Task<T?> GetJsonAsync<T>(
        string key,
        SettingsResolutionContext? context = null,
        CancellationToken ct = default);

    Task<IReadOnlyDictionary<string, string?>> GetBatchAsync(
        IReadOnlyList<string> keys,
        SettingsResolutionContext? context = null,
        CancellationToken ct = default);
}
