using Andy.Settings.Api.Controllers;
using Andy.Settings.Application.DTOs.Effective;
using Andy.Settings.Application.Interfaces;
using Andy.Settings.Domain.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Andy.Settings.Tests.Unit.Controllers;

public class EffectiveControllerTests
{
    private readonly Mock<IResolutionService> _serviceMock = new();
    private readonly EffectiveController _sut;

    public EffectiveControllerTests()
    {
        _sut = new EffectiveController(_serviceMock.Object);
    }

    [Fact]
    public async Task Resolve_Returns200WithResolvedSetting()
    {
        var resolved = new ResolvedSetting
        {
            Key = "app.test.key",
            EffectiveValue = "\"value\"",
            WinningScopeType = ScopeType.Machine,
            DataType = SettingDataType.String,
            IsValid = true,
            IsDefault = false
        };
        var request = new ResolveRequest
        {
            Key = "app.test.key",
            Context = new ResolutionContext { UserId = "user1" }
        };
        _serviceMock.Setup(s => s.ResolveAsync("app.test.key", request.Context, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resolved);

        var result = await _sut.Resolve(request, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
        var value = okResult.Value.Should().BeOfType<ResolvedSetting>().Subject;
        value.Key.Should().Be("app.test.key");
        value.EffectiveValue.Should().Be("\"value\"");
    }

    [Fact]
    public async Task Explain_Returns200WithSourceChain()
    {
        var resolved = new ResolvedSetting
        {
            Key = "app.test.key",
            EffectiveValue = "\"user-value\"",
            WinningScopeType = ScopeType.User,
            DataType = SettingDataType.String,
            IsValid = true,
            IsDefault = false,
            SourceChain = new List<SourceChainEntry>
            {
                new(ScopeType.Machine, null, "\"default\"", false),
                new(ScopeType.Machine, null, "\"machine-value\"", false),
                new(ScopeType.User, "user1", "\"user-value\"", true)
            }
        };
        var request = new ResolveRequest
        {
            Key = "app.test.key",
            Context = new ResolutionContext { UserId = "user1" }
        };
        _serviceMock.Setup(s => s.ExplainAsync("app.test.key", request.Context, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resolved);

        var result = await _sut.Explain(request, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
        var value = okResult.Value.Should().BeOfType<ResolvedSetting>().Subject;
        value.SourceChain.Should().HaveCount(3);
        value.SourceChain.Should().Contain(e => e.IsWinner);
    }

    [Fact]
    public async Task ResolveBatch_Returns200WithMultipleResults()
    {
        var results = new List<ResolvedSetting>
        {
            new() { Key = "key1", EffectiveValue = "\"v1\"", IsValid = true },
            new() { Key = "key2", EffectiveValue = "\"v2\"", IsValid = true }
        };
        var request = new ResolveBatchRequest
        {
            Keys = new[] { "key1", "key2" },
            Context = new ResolutionContext { UserId = "user1" }
        };
        _serviceMock.Setup(s => s.ResolveBatchAsync(request.Keys, request.Context, It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);

        var result = await _sut.ResolveBatch(request, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
    }
}
