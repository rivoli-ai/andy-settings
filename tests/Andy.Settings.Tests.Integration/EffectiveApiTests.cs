using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;

namespace Andy.Settings.Tests.Integration;

public class EffectiveApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public EffectiveApiTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    [Fact]
    public async Task Resolve_ReturnsOkWithEffectiveValue()
    {
        var request = new
        {
            key = "andy.containers.defaultProvider",
            context = new
            {
                applicationCode = "containers"
            }
        };

        var response = await _client.PostAsJsonAsync("/api/effective/resolve", request, _jsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        json.GetProperty("key").GetString().Should().Be("andy.containers.defaultProvider");
        // The effective value should be the default value since no assignments override it
        json.GetProperty("effectiveValue").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Explain_ReturnsOkWithSourceChain()
    {
        var request = new
        {
            key = "andy.containers.defaultProvider",
            context = new
            {
                applicationCode = "containers"
            }
        };

        var response = await _client.PostAsJsonAsync("/api/effective/explain", request, _jsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        json.GetProperty("key").GetString().Should().Be("andy.containers.defaultProvider");
        json.GetProperty("sourceChain").GetArrayLength().Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task SettingUserScopedValue_ChangesEffectiveValueForThatUser()
    {
        const string key = "andy.containers.defaultProvider";
        const string userId = "test-user-123";

        // First, resolve the default value
        var resolveRequest = new
        {
            key,
            context = new
            {
                userId,
                applicationCode = "containers"
            }
        };

        var beforeResponse = await _client.PostAsJsonAsync("/api/effective/resolve", resolveRequest, _jsonOptions);
        beforeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var beforeJson = await beforeResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var defaultValue = beforeJson.GetProperty("effectiveValue").GetString();

        // Set a user-scoped value
        var setDto = new
        {
            definitionKey = key,
            scopeType = "User",
            scopeId = userId,
            valueJson = "\"kubernetes\""
        };
        var setResponse = await _client.PostAsJsonAsync("/api/values", setDto, _jsonOptions);
        setResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Resolve again -- the effective value should now be the user-scoped value
        var afterResponse = await _client.PostAsJsonAsync("/api/effective/resolve", resolveRequest, _jsonOptions);
        afterResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterJson = await afterResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);

        afterJson.GetProperty("effectiveValue").GetString().Should().Be("\"kubernetes\"");
        afterJson.GetProperty("effectiveValue").GetString().Should().NotBe(defaultValue,
            "the user-scoped value should override the default");
    }
}
