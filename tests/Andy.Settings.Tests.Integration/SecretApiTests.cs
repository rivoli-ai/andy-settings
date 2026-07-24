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
    public async Task OrdinaryValuesEndpoint_RejectsSecretPlaintext()
    {
        var key = $"test.secret.boundary.{Guid.NewGuid():N}";
        await CreateSecretDefinitionAsync(key);

        var response = await _client.PostAsJsonAsync("/api/values", new
        {
            definitionKey = key,
            scopeType = "Machine",
            valueJson = "\"must-not-be-stored\""
        }, _jsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var listed = await _client.GetFromJsonAsync<JsonElement>(
            $"/api/values?definitionKey={Uri.EscapeDataString(key)}", _jsonOptions);
        listed.GetProperty("totalCount").GetInt32().Should().Be(0);
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
    public async Task DeleteSecret_ScopedDelete_ReturnsNoContent()
    {
        var key = $"test.secret.delete.{Guid.NewGuid():N}";
        await CreateSecretDefinitionAsync(key);

        // Set a secret first
        var setBody = new { scopeType = "Machine", value = "to-be-deleted" };
        var setResponse = await _client.PostAsJsonAsync($"/api/secrets/{key}", setBody, _jsonOptions);
        setResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var deleteResponse = await _client.DeleteAsync($"/api/secrets/{key}?scopeType=Machine");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it's gone
        var getResponse = await _client.GetAsync($"/api/secrets/{key}?scopeType=Machine");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // rivoli-ai/andy-settings#138. A bare DELETE used to wipe every scope for
    // the definition. It is now rejected rather than silently reinterpreted as
    // a narrower delete, which would leave the caller believing a still-stored
    // secret was gone.
    [Fact]
    public async Task DeleteSecret_WithoutScope_ReturnsBadRequestAndDeletesNothing()
    {
        var key = $"test.secret.delete.{Guid.NewGuid():N}";
        await CreateSecretDefinitionAsync(key);
        await _client.PostAsJsonAsync($"/api/secrets/{key}",
            new { scopeType = "Machine", value = "still-here" }, _jsonOptions);

        var deleteResponse = await _client.DeleteAsync($"/api/secrets/{key}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var getResponse = await _client.GetAsync($"/api/secrets/{key}?scopeType=Machine");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK, "nothing may be deleted by an ambiguous request");
    }

    [Fact]
    public async Task DeleteSecret_AllScopes_RemovesEveryScope()
    {
        var key = $"test.secret.delete.{Guid.NewGuid():N}";
        await CreateSecretDefinitionAsync(key);
        await _client.PostAsJsonAsync($"/api/secrets/{key}",
            new { scopeType = "Machine", value = "machine-value" }, _jsonOptions);
        await _client.PostAsJsonAsync($"/api/secrets/{key}",
            new { scopeType = "User", scopeId = "user1", value = "user-value" }, _jsonOptions);

        var deleteResponse = await _client.DeleteAsync($"/api/secrets/{key}?allScopes=true");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await _client.GetAsync($"/api/secrets/{key}?scopeType=Machine")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
        (await _client.GetAsync($"/api/secrets/{key}?scopeType=User&scopeId=user1")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }

    // The whole point of the change: one user's secret can be cleared without
    // taking out everyone else's.
    [Fact]
    public async Task DeleteSecret_ScopedDelete_LeavesOtherScopesIntact()
    {
        var key = $"test.secret.delete.{Guid.NewGuid():N}";
        await CreateSecretDefinitionAsync(key);
        await _client.PostAsJsonAsync($"/api/secrets/{key}",
            new { scopeType = "Machine", value = "machine-value" }, _jsonOptions);
        await _client.PostAsJsonAsync($"/api/secrets/{key}",
            new { scopeType = "User", scopeId = "user1", value = "user1-value" }, _jsonOptions);
        await _client.PostAsJsonAsync($"/api/secrets/{key}",
            new { scopeType = "User", scopeId = "user2", value = "user2-value" }, _jsonOptions);

        var deleteResponse = await _client.DeleteAsync($"/api/secrets/{key}?scopeType=User&scopeId=user1");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await _client.GetAsync($"/api/secrets/{key}?scopeType=User&scopeId=user1")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
        (await _client.GetAsync($"/api/secrets/{key}?scopeType=User&scopeId=user2")).StatusCode
            .Should().Be(HttpStatusCode.OK, "another user's secret must survive");
        (await _client.GetAsync($"/api/secrets/{key}?scopeType=Machine")).StatusCode
            .Should().Be(HttpStatusCode.OK, "the machine-scope secret must survive");
    }
}
