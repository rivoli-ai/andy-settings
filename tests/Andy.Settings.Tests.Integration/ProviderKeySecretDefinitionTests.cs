using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;

namespace Andy.Settings.Tests.Integration;

/// <summary>
/// rivoli-ai/conductor#2126. Proves the provider API-key write path
/// works end-to-end against a FRESH database seeded only from the
/// registration manifests.
///
/// Root cause being pinned: <c>SecretService.SetSecretAsync</c>
/// requires a pre-existing <c>SettingDefinition</c> row and returns
/// 404 otherwise. Conductor writes provider keys as
/// <c>andy.models.providers.&lt;slug&gt;.apiKey</c> (Machine scope),
/// but andy-models' manifest used to declare only the legacy
/// <c>andy.models.defaults.*</c> keys — so every key write 404'd and
/// users "lost" their Anthropic key. The fixture
/// <c>Fixtures/registrations/andy-models.json</c> mirrors the updated
/// andy-models <c>config/registration.json</c>; these tests fail when
/// that fixture lacks the per-provider secret definitions.
/// </summary>
public class ProviderKeySecretDefinitionTests : IClassFixture<CustomWebApplicationFactory>
{
    // Mirror of the andy-models catalog slugs (canonical
    // `config/models-seed.json` ∪ legacy `providers.json`), which the
    // andy-models unit suite (`ProviderKeyDefinitionManifestTests`)
    // pins against the same manifest this fixture mirrors.
    private static readonly string[] CatalogProviderSlugs =
    {
        "anthropic", "aws-bedrock", "azure-openai", "cerebras", "google",
        "groq", "mistral", "ollama", "openai", "openai-compatible",
        "openai-compatible-generic", "openrouter", "vllm",
    };

    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public ProviderKeySecretDefinitionTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    [Fact]
    public async Task SetAnthropicProviderKey_WithManifestSeededDefinition_Succeeds()
    {
        // This is byte-for-byte the request Conductor's
        // AndyAuthSettingsAdapter sends when the user enters an
        // Anthropic API key. Before #2126 it returned 404 because no
        // definition existed for the key.
        const string key = "andy.models.providers.anthropic.apiKey";

        var body = new { scopeType = "Machine", value = "sk-ant-integration-test" };
        var response = await _client.PostAsJsonAsync($"/api/secrets/{key}", body, _jsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "the manifest-seeded definition must accept Conductor's Machine-scope key write");

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        json.GetProperty("definitionKey").GetString().Should().Be(key);

        // Read-back: the key reports configured with the stored value.
        var getResponse = await _client.GetAsync($"/api/secrets/{key}?scopeType=Machine");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var getJson = await getResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        getJson.GetProperty("value").GetString().Should().Be("sk-ant-integration-test");
    }

    [Fact]
    public async Task EveryCatalogProviderSlug_AcceptsAKeyWrite()
    {
        foreach (var slug in CatalogProviderSlugs)
        {
            var key = $"andy.models.providers.{slug}.apiKey";
            var body = new { scopeType = "Machine", value = $"test-key-{slug}" };

            var response = await _client.PostAsJsonAsync($"/api/secrets/{key}", body, _jsonOptions);

            response.StatusCode.Should().Be(HttpStatusCode.Created,
                $"'{key}' must have a manifest-seeded secret definition — a 404 here is the conductor#2126 lost-key bug");
        }
    }

    [Fact]
    public async Task SetProviderKey_ForUndeclaredSlug_StillReturns404()
    {
        // Pins the mechanism that caused the original bug: writes to
        // keys without a seeded definition are rejected, not
        // auto-created. If this ever flips to auto-create, the
        // definition seeding above becomes optional and the manifest
        // pin tests in andy-models can be retired.
        var body = new { scopeType = "Machine", value = "irrelevant" };
        var response = await _client.PostAsJsonAsync(
            "/api/secrets/andy.models.providers.not-a-real-provider.apiKey", body, _jsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
