using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;

namespace Andy.Settings.Tests.Integration;

public class DefinitionApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public DefinitionApiTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    [Fact]
    public async Task ListDefinitions_ReturnsOkWithSeededData()
    {
        var response = await _client.GetAsync("/api/definitions?pageSize=50");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var items = json.GetProperty("items");
        items.GetArrayLength().Should().BeGreaterOrEqualTo(25,
            "the DataSeeder creates 25 definitions");
    }

    [Fact]
    public async Task GetDefinition_WithExistingKey_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/definitions/andy.containers.defaultProvider");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        json.GetProperty("key").GetString().Should().Be("andy.containers.defaultProvider");
        json.GetProperty("applicationCode").GetString().Should().Be("containers");
    }

    [Fact]
    public async Task GetDefinition_WithNonExistentKey_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/definitions/nonexistent.key.here");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateDefinition_ReturnsCreated()
    {
        var dto = new
        {
            key = "test.integration.newSetting",
            applicationCode = "test",
            displayName = "Test Setting",
            description = "A setting created by integration tests",
            category = "Testing",
            dataType = "String",
            defaultValueJson = "\"hello\""
        };

        var response = await _client.PostAsJsonAsync("/api/definitions", dto, _jsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        json.GetProperty("key").GetString().Should().Be("test.integration.newSetting");
        json.GetProperty("displayName").GetString().Should().Be("Test Setting");
    }

    [Fact]
    public async Task UpdateDefinition_WithExistingKey_ReturnsOk()
    {
        var updateDto = new
        {
            displayName = "Updated Default Provider",
            description = "Updated description for integration test"
        };

        var response = await _client.PutAsJsonAsync(
            "/api/definitions/andy.containers.defaultProvider", updateDto, _jsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        json.GetProperty("displayName").GetString().Should().Be("Updated Default Provider");
    }

    [Fact]
    public async Task DeleteDefinition_WithExistingKey_ReturnsNoContent()
    {
        // First create a definition to delete
        var createDto = new
        {
            key = "test.integration.toDelete",
            applicationCode = "test",
            displayName = "To Delete",
            dataType = "String"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/definitions", createDto, _jsonOptions);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Now delete it
        var response = await _client.DeleteAsync("/api/definitions/test.integration.toDelete");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it's gone
        var getResponse = await _client.GetAsync("/api/definitions/test.integration.toDelete");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
