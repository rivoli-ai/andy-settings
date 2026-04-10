using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;

namespace Andy.Settings.Tests.Integration;

public class ExportApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public ExportApiTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    [Fact]
    public async Task Export_ReturnsOkWithData()
    {
        var response = await _client.GetAsync("/api/export");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        json.GetProperty("format").GetString().Should().Be("json");
        json.GetProperty("definitionCount").GetInt32().Should().BeGreaterOrEqualTo(25);
        json.GetProperty("data").GetString().Should().NotBeNullOrEmpty();
    }
}
