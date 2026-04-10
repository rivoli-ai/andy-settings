using System;
using System.Net.Http;
using System.Net.Http.Headers;

namespace Andy.Settings.Cli;

public static class HttpClientFactory
{
    public static HttpClient Create(string baseUrl)
    {
        var handler = new HttpClientHandler
        {
            // Skip SSL validation for development
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/")
        };

        var token = Environment.GetEnvironmentVariable("ANDY_SETTINGS_TOKEN");
        if (!string.IsNullOrWhiteSpace(token))
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        return client;
    }
}
