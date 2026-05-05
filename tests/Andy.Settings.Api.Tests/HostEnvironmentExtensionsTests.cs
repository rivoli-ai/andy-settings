using Andy.Settings.Api;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Andy.Settings.Api.Tests;

public class HostEnvironmentExtensionsTests
{
    [Theory]
    [InlineData("Embedded", true)]
    [InlineData("Development", false)]
    [InlineData("Production", false)]
    public void IsEmbedded_ReturnsExpected(string envName, bool expected)
        => Assert.Equal(expected, new FakeEnv(envName).IsEmbedded());

    [Fact]
    public void EmbeddedEnvironmentName_MatchesConductorContract()
        => Assert.Equal("Embedded", HostEnvironmentExtensions.EmbeddedEnvironmentName);

    private sealed class FakeEnv : IHostEnvironment
    {
        public FakeEnv(string name) => EnvironmentName = name;
        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = "/";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
