// Copyright (c) Rivoli AI 2026. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Andy.Settings.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Andy.Settings.Client;

internal sealed class AndySettingsHttpClient : IAndySettingsClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly IAndySettingsTokenProvider _tokenProvider;
    private readonly IOptionsMonitor<AndySettingsOptions> _options;
    private readonly ILogger<AndySettingsHttpClient> _logger;
    private readonly ConcurrentDictionary<string, byte> _warnedFallbacks = new();

    public AndySettingsHttpClient(
        HttpClient httpClient,
        IAndySettingsTokenProvider tokenProvider,
        IOptionsMonitor<AndySettingsOptions> options,
        ILogger<AndySettingsHttpClient> logger)
    {
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
        _options = options;
        _logger = logger;
    }

    public async Task<string?> GetStringAsync(
        string key,
        SettingsResolutionContext? context = null,
        CancellationToken ct = default)
    {
        var resolved = await TryResolveAsync(key, context, ct);
        return resolved ?? GetFallback(key);
    }

    public async Task<int?> GetIntAsync(
        string key,
        SettingsResolutionContext? context = null,
        CancellationToken ct = default)
    {
        var raw = await GetStringAsync(key, context, ct);
        return int.TryParse(raw, out var value) ? value : null;
    }

    public async Task<bool?> GetBoolAsync(
        string key,
        SettingsResolutionContext? context = null,
        CancellationToken ct = default)
    {
        var raw = await GetStringAsync(key, context, ct);
        return bool.TryParse(raw, out var value) ? value : null;
    }

    public async Task<T?> GetJsonAsync<T>(
        string key,
        SettingsResolutionContext? context = null,
        CancellationToken ct = default)
    {
        var raw = await GetStringAsync(key, context, ct);
        if (string.IsNullOrEmpty(raw))
            return default;

        try
        {
            return JsonSerializer.Deserialize<T>(raw, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize setting '{Key}' as {Type}", key, typeof(T).Name);
            return default;
        }
    }

    public async Task<IReadOnlyDictionary<string, string?>> GetBatchAsync(
        IReadOnlyList<string> keys,
        SettingsResolutionContext? context = null,
        CancellationToken ct = default)
    {
        if (keys.Count == 0)
            return new Dictionary<string, string?>();

        var remote = await TryResolveBatchAsync(keys, context, ct);
        if (remote is not null)
        {
            var withFallback = new Dictionary<string, string?>(remote.Count);
            foreach (var key in keys)
            {
                withFallback[key] = remote.TryGetValue(key, out var value) && value is not null
                    ? value
                    : GetFallback(key);
            }
            return withFallback;
        }

        var fallback = new Dictionary<string, string?>(keys.Count);
        foreach (var key in keys)
            fallback[key] = GetFallback(key);
        return fallback;
    }

    private async Task<string?> TryResolveAsync(
        string key,
        SettingsResolutionContext? context,
        CancellationToken ct)
    {
        if (_httpClient.BaseAddress is null)
            return null;

        try
        {
            using var request = await BuildRequestAsync(
                HttpMethod.Post,
                "/api/effective/resolve",
                new { key, context = BuildContext(context) },
                ct);

            using var response = await _httpClient.SendAsync(request, ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();

            var resolved = await response.Content.ReadFromJsonAsync<ResolvedSettingResponse>(JsonOptions, ct);
            return resolved?.EffectiveValue;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            WarnOnce(key, ex);
            return null;
        }
    }

    private async Task<Dictionary<string, string?>?> TryResolveBatchAsync(
        IReadOnlyList<string> keys,
        SettingsResolutionContext? context,
        CancellationToken ct)
    {
        if (_httpClient.BaseAddress is null)
            return null;

        try
        {
            using var request = await BuildRequestAsync(
                HttpMethod.Post,
                "/api/effective/resolve-batch",
                new { keys, context = BuildContext(context) },
                ct);

            using var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            // andy-settings returns the result list directly at the top level
            // (no envelope). Accept both shapes to be defensive.
            var raw = await response.Content.ReadAsStringAsync(ct);
            IReadOnlyList<ResolvedSettingResponse>? items = null;
            try
            {
                items = JsonSerializer.Deserialize<IReadOnlyList<ResolvedSettingResponse>>(raw, JsonOptions);
            }
            catch (JsonException)
            {
                var wrapped = JsonSerializer.Deserialize<ResolvedBatchResponse>(raw, JsonOptions);
                items = wrapped?.Results;
            }

            if (items is null)
                return null;

            var map = new Dictionary<string, string?>(items.Count);
            foreach (var entry in items)
                map[entry.Key] = entry.EffectiveValue;
            return map;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            WarnOnce($"batch:{string.Join(',', keys)}", ex);
            return null;
        }
    }

    private async Task<HttpRequestMessage> BuildRequestAsync(
        HttpMethod method,
        string relativeUri,
        object body,
        CancellationToken ct)
    {
        var request = new HttpRequestMessage(method, relativeUri)
        {
            Content = JsonContent.Create(body, options: JsonOptions),
        };

        var token = await _tokenProvider.GetTokenAsync(ct);
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return request;
    }

    private object BuildContext(SettingsResolutionContext? context)
    {
        var options = _options.CurrentValue;
        return new
        {
            userId = context?.UserId,
            teamId = context?.TeamId,
            workspaceId = context?.WorkspaceId,
            applicationCode = context?.ApplicationCode ?? options.ApplicationCode,
            serviceCode = context?.ServiceCode,
        };
    }

    private string? GetFallback(string key)
    {
        return _options.CurrentValue.Defaults.TryGetValue(key, out var value) ? value : null;
    }

    private void WarnOnce(string key, Exception ex)
    {
        if (_warnedFallbacks.TryAdd(key, 0))
            _logger.LogWarning(
                ex,
                "Falling back to local default for setting '{Key}' because andy-settings is unreachable",
                key);
    }

    private sealed record ResolvedSettingResponse(
        string Key,
        string? EffectiveValue,
        bool IsDefault,
        bool IsValid);

    private sealed record ResolvedBatchResponse(IReadOnlyList<ResolvedSettingResponse> Results);
}
