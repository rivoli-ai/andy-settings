using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;

namespace Andy.Settings.Tests.Integration;

public class AuditApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public AuditApiTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    [Fact]
    public async Task GetAudit_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/audit");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        json.GetProperty("items").ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task AfterSettingValue_AuditContainsCreatedEvent()
    {
        // Set a value to trigger an audit event
        var setDto = new
        {
            definitionKey = "andy.auth.tokenLifetimeMinutes",
            scopeType = "Machine",
            valueJson = "120"
        };
        var setResponse = await _client.PostAsJsonAsync("/api/values", setDto, _jsonOptions);
        setResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Query audit events for that key
        var response = await _client.GetAsync("/api/audit?definitionKey=andy.auth.tokenLifetimeMinutes");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var items = json.GetProperty("items");
        items.GetArrayLength().Should().BeGreaterOrEqualTo(1);

        // At least one event should be a Created event
        var hasCreatedEvent = false;
        foreach (var item in items.EnumerateArray())
        {
            var eventType = item.GetProperty("eventType").GetString();
            if (eventType == "Created")
            {
                hasCreatedEvent = true;
                break;
            }
        }
        hasCreatedEvent.Should().BeTrue("setting a value for the first time should create a 'Created' audit event");
    }
}
