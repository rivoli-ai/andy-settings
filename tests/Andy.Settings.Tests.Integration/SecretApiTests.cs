using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;

namespace Andy.Settings.Tests.Integration;

public class SecretApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public SecretApiTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    private async Task<string> CreateSecretDefinitionAsync(string key)
    {
        var dto = new
        {
            key,
            applicationCode = "test",
            displayName = "Integration Test Secret",
            description = "A secret-type setting for integration tests",
            category = "Testing",
            dataType = "Secret",
            isSecret = true
        };

        var response = await _client.PostAsJsonAsync("/api/definitions", dto, _jsonOptions);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return key;
    }

    [Fact]
    public async Task SetSecret_ReturnsCreated()
    {
        var key = $"test.secret.set.{Guid.NewGuid():N}";
        await CreateSecretDefinitionAsync(key);

        var body = new { scopeType = "Machine", value = "my-secret-password" };
        var response = await _client.PostAsJsonAsync($"/api/secrets/{key}", body, _jsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        json.GetProperty("definitionKey").GetString().Should().Be(key);
    }

    [Fact]
    public async Task GetSecret_ReturnsValueThatMatchesOriginal()
    {
        var key = $"test.secret.get.{Guid.NewGuid():N}";
        await CreateSecretDefinitionAsync(key);

        // Set the secret
        var setBody = new { scopeType = "Machine", value = "readable-secret" };
        var setResponse = await _client.PostAsJsonAsync($"/api/secrets/{key}", setBody, _jsonOptions);
        setResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Get the secret back
        var getResponse = await _client.GetAsync($"/api/secrets/{key}?scopeType=Machine");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await getResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        json.GetProperty("definitionKey").GetString().Should().Be(key);
        json.GetProperty("value").GetString().Should().Be("readable-secret");
    }

    [Fact]
    public async Task RotateSecret_ReturnsOkWithNewValue()
    {
        var key = $"test.secret.rotate.{Guid.NewGuid():N}";
        await CreateSecretDefinitionAsync(key);

        // Set original secret
        var setBody = new { scopeType = "Machine", value = "original-secret" };
        var setResponse = await _client.PostAsJsonAsync($"/api/secrets/{key}", setBody, _jsonOptions);
        setResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Rotate the secret
        var rotateBody = new { scopeType = "Machine", newValue = "rotated-secret" };
        var rotateResponse = await _client.PostAsJsonAsync($"/api/secrets/{key}/rotate", rotateBody, _jsonOptions);
        rotateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify the new value
        var getResponse = await _client.GetAsync($"/api/secrets/{key}?scopeType=Machine");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await getResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        json.GetProperty("value").GetString().Should().Be("rotated-secret");
    }

    [Fact]
    public async Task DeleteSecret_ReturnsNoContent()
    {
        var key = $"test.secret.delete.{Guid.NewGuid():N}";
        await CreateSecretDefinitionAsync(key);

        // Set a secret first
        var setBody = new { scopeType = "Machine", value = "to-be-deleted" };
        var setResponse = await _client.PostAsJsonAsync($"/api/secrets/{key}", setBody, _jsonOptions);
        setResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Delete the secret
        var deleteResponse = await _client.DeleteAsync($"/api/secrets/{key}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it's gone
        var getResponse = await _client.GetAsync($"/api/secrets/{key}?scopeType=Machine");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
