using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;

namespace Andy.Settings.Tests.Integration;

public class ValueApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public ValueApiTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    [Fact]
    public async Task SetValue_ReturnsOk()
    {
        var dto = new
        {
            definitionKey = "andy.containers.defaultProvider",
            scopeType = "Machine",
            valueJson = "\"podman\""
        };

        var response = await _client.PostAsJsonAsync("/api/values", dto, _jsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        json.GetProperty("definitionKey").GetString().Should().Be("andy.containers.defaultProvider");
        json.GetProperty("valueJson").GetString().Should().Be("\"podman\"");
    }

    [Fact]
    public async Task ListValues_ReturnsOk()
    {
        // Set a value first so there's data
        var setDto = new
        {
            definitionKey = "andy.containers.maxCpuCores",
            scopeType = "Machine",
            valueJson = "8"
        };
        await _client.PostAsJsonAsync("/api/values", setDto, _jsonOptions);

        var response = await _client.GetAsync("/api/values");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        json.GetProperty("items").GetArrayLength().Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task DeleteValue_ReturnsNoContent()
    {
        // Set a value first
        var setDto = new
        {
            definitionKey = "andy.containers.sshTimeoutSeconds",
            scopeType = "Machine",
            valueJson = "60"
        };
        var setResponse = await _client.PostAsJsonAsync("/api/values", setDto, _jsonOptions);
        setResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await setResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var id = json.GetProperty("id").GetString();

        // Delete it
        var response = await _client.DeleteAsync($"/api/values/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
