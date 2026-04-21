// Copyright (c) Rivoli AI 2026. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Immutable;

namespace Andy.Settings.Client;

public sealed class SettingsSnapshot : ISettingsSnapshot
{
    private ImmutableDictionary<string, string?> _values =
        ImmutableDictionary<string, string?>.Empty.WithComparers(StringComparer.OrdinalIgnoreCase);

    private DateTimeOffset? _lastRefreshedAt;

    public DateTimeOffset? LastRefreshedAt => _lastRefreshedAt;

    public IReadOnlyCollection<string> Keys => _values.Keys.ToArray();

    public string? GetString(string key)
        => _values.TryGetValue(key, out var value) ? value : null;

    public int? GetInt(string key)
        => int.TryParse(GetString(key), out var value) ? value : null;

    public bool? GetBool(string key)
        => bool.TryParse(GetString(key), out var value) ? value : null;

    public void Update(IReadOnlyDictionary<string, string?> values)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in values)
            builder[key] = value;

        _values = builder.ToImmutable();
        _lastRefreshedAt = DateTimeOffset.UtcNow;
    }
}
