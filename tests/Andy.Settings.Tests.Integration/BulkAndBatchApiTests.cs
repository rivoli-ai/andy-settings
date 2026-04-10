using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;

namespace Andy.Settings.Tests.Integration;

public class BulkAndBatchApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public BulkAndBatchApiTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    [Fact]
    public async Task BulkSet_ReturnsNoContent()
    {
        var dtos = new[]
        {
            new
            {
                definitionKey = "andy.containers.defaultProvider",
                scopeType = "Machine",
                valueJson = "\"podman\""
            },
            new
            {
                definitionKey = "andy.containers.maxCpuCores",
                scopeType = "Machine",
                valueJson = "16"
            }
        };

        var response = await _client.PostAsJsonAsync("/api/values/bulk", dtos, _jsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ResolveBatch_ReturnsMultipleResults()
    {
        var request = new
        {
            keys = new[] { "andy.containers.defaultProvider", "andy.containers.maxCpuCores" },
            context = new
            {
                applicationCode = "containers"
            }
        };

        var response = await _client.PostAsJsonAsync("/api/effective/resolve-batch", request, _jsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        json.GetArrayLength().Should().Be(2);
    }
}
