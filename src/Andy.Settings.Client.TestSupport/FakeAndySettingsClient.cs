// Copyright (c) Rivoli AI 2026. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;

namespace Andy.Settings.Client.TestSupport;

/// <summary>
/// Canonical in-memory fake of <see cref="IAndySettingsClient"/> for
/// consumer integration tests. Dict-backed, thread-safe, implements
/// every method on the interface. Replaces the per-service
/// <c>FakeAndySettingsClient</c> / <c>StubAndySettingsClient</c>
/// doubles that andy-tasks / andy-issues / andy-agents ship today.
///
/// Usage:
/// <code>
/// var fake = new FakeAndySettingsClient();
/// fake.Set("andy.issues.integrations.githubPat", "ghp_xxx");
/// services.AddSingleton&lt;IAndySettingsClient&gt;(fake);
/// </code>
/// </summary>
public class FakeAndySettingsClient : IAndySettingsClient
{
    private readonly Dictionary<string, string?> _store = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    /// <summary>Seeds a value for <paramref name="key"/>.</summary>
    public FakeAndySettingsClient Set(string key, string? value)
    {
        lock (_gate) { _store[key] = value; }
        return this;
    }

    /// <summary>Removes a key (behaves like "never set").</summary>
    public bool Remove(string key)
    {
        lock (_gate) { return _store.Remove(key); }
    }

    /// <summary>Clears every seeded value. Call between test cases.</summary>
    public void Reset()
    {
        lock (_gate) { _store.Clear(); }
    }

    /// <summary>Returns every seeded (key, value) — useful for test assertions.</summary>
    public IReadOnlyDictionary<string, string?> Snapshot()
    {
        lock (_gate) { return new Dictionary<string, string?>(_store); }
    }

    public Task<string?> GetStringAsync(
        string key,
        SettingsResolutionContext? context = null,
        CancellationToken ct = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_store.TryGetValue(key, out var v) ? v : null);
        }
    }

    public async Task<int?> GetIntAsync(
        string key,
        SettingsResolutionContext? context = null,
        CancellationToken ct = default)
    {
        var raw = await GetStringAsync(key, context, ct).ConfigureAwait(false);
        return int.TryParse(raw, out var i) ? i : null;
    }

    public async Task<bool?> GetBoolAsync(
        string key,
        SettingsResolutionContext? context = null,
        CancellationToken ct = default)
    {
        var raw = await GetStringAsync(key, context, ct).ConfigureAwait(false);
        if (raw is null) return null;
        if (bool.TryParse(raw, out var b)) return b;
        return null;
    }

    public async Task<T?> GetJsonAsync<T>(
        string key,
        SettingsResolutionContext? context = null,
        CancellationToken ct = default)
    {
        var raw = await GetStringAsync(key, context, ct).ConfigureAwait(false);
        if (raw is null) return default;
        try
        {
            return JsonSerializer.Deserialize<T>(raw);
        }
        catch
        {
            return default;
        }
    }

    public Task<IReadOnlyDictionary<string, string?>> GetBatchAsync(
        IReadOnlyList<string> keys,
        SettingsResolutionContext? context = null,
        CancellationToken ct = default)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        lock (_gate)
        {
            foreach (var key in keys)
            {
                if (_store.TryGetValue(key, out var v))
                    result[key] = v;
            }
        }
        return Task.FromResult<IReadOnlyDictionary<string, string?>>(result);
    }
}
