// Copyright (c) Rivoli AI 2026. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Andy.Settings.Client;

public sealed record SettingDefinitionView(
    string Key,
    string DisplayName,
    string? Description,
    string DataType,
    string? DefaultValueJson,
    bool IsSecret,
    string? Category);

public sealed record SettingValueView(
    string Key,
    string DisplayName,
    string? Description,
    string DataType,
    string? DefaultValueJson,
    bool IsSecret,
    string? Category,
    string? EffectiveValue,
    bool HasEffectiveValue);

public sealed record SetSettingValueRequest(
    string ValueJson,
    string ScopeType,
    string? ScopeId);

public interface ISettingsAdminClient
{
    Task<IReadOnlyList<SettingDefinitionView>> ListDefinitionsAsync(
        string applicationCode,
        CancellationToken ct = default);

    Task<bool> SetValueAsync(
        string key,
        SetSettingValueRequest request,
        CancellationToken ct = default);
}
