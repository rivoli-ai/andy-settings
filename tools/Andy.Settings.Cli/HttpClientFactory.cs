using System;
using System.Net.Http;
using System.Net.Http.Headers;

namespace Andy.Settings.Cli;

public static class HttpClientFactory
{
    /// <summary>
    /// Environment variable form of <c>--insecure</c>, for scripted use where
    /// passing the flag on every invocation is impractical.
    /// </summary>
    public const string InsecureEnvVar = "ANDY_SETTINGS_INSECURE";

    private static bool _allowInsecureTls;
    private static bool _warned;

    /// <summary>
    /// Disables TLS certificate validation for every client created afterwards.
    /// Set once at startup from the global <c>--insecure</c> option or
    /// <see cref="InsecureEnvVar"/>.
    /// </summary>
    /// <remarks>
    /// This CLI carries bearer tokens and plaintext secret values, so
    /// certificate validation is on by default and the bypass has to be asked
    /// for explicitly. It used to be unconditional
    /// (rivoli-ai/andy-settings#129), which made every invocation — in every
    /// environment, against any host — trivially interceptable.
    /// </remarks>
    public static void AllowInsecureTls() => _allowInsecureTls = true;

    /// <summary>Reads the env-var opt-in. Call once at startup.</summary>
    public static bool InsecureRequestedViaEnvironment()
    {
        var value = Environment.GetEnvironmentVariable(InsecureEnvVar);
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1";
    }

    // Test seam: resets process-wide state between cases.
    internal static void ResetForTests()
    {
        _allowInsecureTls = false;
        _warned = false;
    }

    internal static bool IsInsecureTlsAllowed => _allowInsecureTls;

    public static HttpClient Create(string baseUrl)
    {
        var handler = new HttpClientHandler();

        if (_allowInsecureTls)
        {
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

            // Warn once per process, not once per request.
            if (!_warned)
            {
                _warned = true;
                Console.Error.WriteLine(
                    "WARNING: TLS certificate validation is disabled. Bearer tokens and " +
                    "secret values sent by this command can be intercepted. Do not use " +
                    "this against a production host.");
            }
        }

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
