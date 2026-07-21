using System.Net;
using System.Text;
using Andy.Settings.Client;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Andy.Settings.Client.Tests;

public sealed class AndySettingsHttpClientTests
{
    [Fact]
    public async Task GetStringAsync_DecodesJsonStringFromApi()
    {
        var sut = CreateClient("\"remote value\"");

        var value = await sut.GetStringAsync("test.key");

        value.Should().Be("remote value");
    }

    [Fact]
    public async Task GetJsonAsync_PreservesJsonForTypedDeserialization()
    {
        var sut = CreateClient("{\"enabled\":true}");

        var value = await sut.GetJsonAsync<TestValue>("test.key");

        value.Should().BeEquivalentTo(new TestValue(true));
    }

    private static AndySettingsHttpClient CreateClient(string effectiveValue)
    {
        var body = "{\"key\":\"test.key\",\"effectiveValue\":" +
                   System.Text.Json.JsonSerializer.Serialize(effectiveValue) +
                   ",\"isDefault\":false,\"isValid\":true}";
        var http = new HttpClient(new StubHandler(body)) { BaseAddress = new Uri("https://settings.test") };
        return new AndySettingsHttpClient(
            http, new NullTokenProvider(), new StaticOptionsMonitor(new AndySettingsOptions()),
            NullLogger<AndySettingsHttpClient>.Instance);
    }

    private sealed record TestValue(bool Enabled);

    private sealed class NullTokenProvider : IAndySettingsTokenProvider
    {
        public Task<string?> GetTokenAsync(CancellationToken ct = default) => Task.FromResult<string?>(null);
    }

    private sealed class StubHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
    }

    private sealed class StaticOptionsMonitor(AndySettingsOptions value) : IOptionsMonitor<AndySettingsOptions>
    {
        public AndySettingsOptions CurrentValue => value;
        public AndySettingsOptions Get(string? name) => value;
        public IDisposable? OnChange(Action<AndySettingsOptions, string?> listener) => null;
    }
}
