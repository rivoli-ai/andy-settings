// Copyright (c) Rivoli AI 2026. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Andy.Settings.Client;
using Microsoft.Extensions.Logging;

namespace Andy.Settings.Client;

internal sealed class SettingsAdminHttpClient : ISettingsAdminClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly IAndySettingsTokenProvider _tokenProvider;
    private readonly ILogger<SettingsAdminHttpClient> _logger;

    public SettingsAdminHttpClient(
        HttpClient httpClient,
        IAndySettingsTokenProvider tokenProvider,
        ILogger<SettingsAdminHttpClient> logger)
    {
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SettingDefinitionView>> ListDefinitionsAsync(
        string applicationCode,
        CancellationToken ct = default)
    {
        if (_httpClient.BaseAddress is null)
            return Array.Empty<SettingDefinitionView>();

        try
        {
            using var request = await BuildRequestAsync(
                HttpMethod.Get,
                $"/api/definitions?applicationCode={Uri.EscapeDataString(applicationCode)}&pageSize=500",
                content: null,
                ct);
            using var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            var paged = await response.Content.ReadFromJsonAsync<PagedDefinitionResponse>(JsonOptions, ct);
            if (paged?.Items is null) return Array.Empty<SettingDefinitionView>();

            return paged.Items
                .Select(d => new SettingDefinitionView(
                    d.Key,
                    d.DisplayName,
                    d.Description,
                    d.DataType,
                    d.DefaultValueJson,
                    d.IsSecret,
                    d.Category))
                .ToList();
        }
        catch (Exception ex) when (
            ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Failed to list setting definitions for {App}", applicationCode);
            return Array.Empty<SettingDefinitionView>();
        }
    }

    public async Task<bool> SetValueAsync(
        string key,
        SetSettingValueRequest request,
        CancellationToken ct = default)
    {
        if (_httpClient.BaseAddress is null) return false;

        try
        {
            using var httpRequest = await BuildRequestAsync(
                HttpMethod.Post,
                "/api/values",
                JsonContent.Create(new
                {
                    definitionKey = key,
                    scopeType = request.ScopeType,
                    scopeId = request.ScopeId,
                    valueJson = request.ValueJson,
                }, options: JsonOptions),
                ct);
            using var response = await _httpClient.SendAsync(httpRequest, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (
            ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Failed to set setting '{Key}'", key);
            return false;
        }
    }

    private async Task<HttpRequestMessage> BuildRequestAsync(
        HttpMethod method,
        string relativeUri,
        HttpContent? content,
        CancellationToken ct)
    {
        var request = new HttpRequestMessage(method, relativeUri);
        if (content is not null) request.Content = content;

        var token = await _tokenProvider.GetTokenAsync(ct);
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return request;
    }

    private sealed record PagedDefinitionResponse(IReadOnlyList<DefinitionPayload>? Items);

    private sealed record DefinitionPayload(
        string Key,
        string DisplayName,
        string? Description,
        string? Category,
        string DataType,
        string? DefaultValueJson,
        bool IsSecret);
}
