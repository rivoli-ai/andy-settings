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

    [Fact]
    public async Task PreviewAndImport_AppliesValidatedDocument()
    {
        var key = $"test.import.{Guid.NewGuid():N}";
        var document = new
        {
            definitions = new[]
            {
                new
                {
                    key,
                    applicationCode = "test",
                    displayName = "Imported setting",
                    dataType = "String",
                    allowedScopesJson = "[\"Machine\"]"
                }
            },
            assignments = new[]
            {
                new { definitionKey = key, scopeType = "Machine", scopeId = (string?)null, valueJson = "\"imported\"" }
            }
        };

        var previewResponse = await _client.PostAsJsonAsync("/api/import/preview", document, _jsonOptions);
        previewResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var preview = await previewResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        preview.GetProperty("isValid").GetBoolean().Should().BeTrue();
        preview.GetProperty("additions").GetArrayLength().Should().Be(2);

        var importResponse = await _client.PostAsJsonAsync("/api/import", document, _jsonOptions);
        importResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await importResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        result.GetProperty("definitionsCreated").GetInt32().Should().Be(1);
        result.GetProperty("assignmentsCreated").GetInt32().Should().Be(1);

        var definitionResponse = await _client.GetAsync($"/api/definitions/{key}");
        definitionResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Preview_RejectsPlaintextSecretAssignments()
    {
        var key = $"test.import.secret.{Guid.NewGuid():N}";
        var document = new
        {
            definitions = new[]
            {
                new { key, applicationCode = "test", displayName = "Secret", dataType = "Secret", isSecret = true }
            },
            assignments = new[]
            {
                new { definitionKey = key, scopeType = "Machine", valueJson = "\"plaintext\"" }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/import/preview", document, _jsonOptions);
        var preview = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        preview.GetProperty("isValid").GetBoolean().Should().BeFalse();
        preview.GetProperty("validationErrors").GetArrayLength().Should().BeGreaterThan(0);
    }
}
