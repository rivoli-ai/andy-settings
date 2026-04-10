using Andy.Settings.Api.Controllers;
using Andy.Settings.Application.DTOs.Secrets;
using Andy.Settings.Application.Interfaces;
using Andy.Settings.Domain.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Andy.Settings.Tests.Unit.Controllers;

public class SecretsControllerTests
{
    private readonly Mock<ISecretService> _secretMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly SecretsController _sut;

    public SecretsControllerTests()
    {
        _currentUserMock.Setup(u => u.GetUserId()).Returns("test-user");
        _sut = new SecretsController(_secretMock.Object, _currentUserMock.Object);
    }

    private static SecretMetadataDto MakeMetadataDto(string definitionKey = "app.secret.key") => new(
        Id: Guid.NewGuid(),
        DefinitionId: Guid.NewGuid(),
        DefinitionKey: definitionKey,
        ScopeType: ScopeType.Machine,
        ScopeId: null,
        UpdatedBy: "test-user",
        CreatedAt: DateTimeOffset.UtcNow,
        UpdatedAt: DateTimeOffset.UtcNow
    );

    [Fact]
    public async Task SetSecret_Returns201OnSuccess()
    {
        var body = new SetSecretBody(ScopeType.Machine, null, "my-secret-value");
        var metadata = MakeMetadataDto();
        _secretMock.Setup(s => s.SetSecretAsync(It.IsAny<SetSecretDto>(), "test-user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(metadata);

        var result = await _sut.SetSecret("app.secret.key", body, CancellationToken.None);

        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.StatusCode.Should().Be(201);
        createdResult.Value.Should().Be(metadata);
    }

    [Fact]
    public async Task SetSecret_Returns404WhenDefinitionNotFound()
    {
        var body = new SetSecretBody(ScopeType.Machine, null, "my-secret-value");
        _secretMock.Setup(s => s.SetSecretAsync(It.IsAny<SetSecretDto>(), "test-user", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Definition not found"));

        var result = await _sut.SetSecret("missing.key", body, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task SetSecret_Returns400WhenDefinitionIsNotSecret()
    {
        var body = new SetSecretBody(ScopeType.Machine, null, "my-secret-value");
        _secretMock.Setup(s => s.SetSecretAsync(It.IsAny<SetSecretDto>(), "test-user", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Definition is not a secret-type setting."));

        var result = await _sut.SetSecret("app.nonsecret.key", body, CancellationToken.None);

        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task GetSecret_Returns200WithValue()
    {
        _secretMock.Setup(s => s.GetSecretAsync(It.IsAny<GetSecretDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("decrypted-value");

        var result = await _sut.GetSecret("app.secret.key", ScopeType.Machine, null, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
        var response = okResult.Value.Should().BeOfType<SecretValueResponse>().Subject;
        response.DefinitionKey.Should().Be("app.secret.key");
        response.Value.Should().Be("decrypted-value");
    }

    [Fact]
    public async Task GetSecret_Returns404WhenNotFound()
    {
        _secretMock.Setup(s => s.GetSecretAsync(It.IsAny<GetSecretDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var result = await _sut.GetSecret("app.secret.key", ScopeType.Machine, null, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task RotateSecret_Returns200OnSuccess()
    {
        var body = new RotateSecretBody(ScopeType.Machine, null, "new-secret-value");
        var metadata = MakeMetadataDto();
        _secretMock.Setup(s => s.RotateSecretAsync(It.IsAny<RotateSecretDto>(), "test-user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(metadata);

        var result = await _sut.RotateSecret("app.secret.key", body, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
        okResult.Value.Should().Be(metadata);
    }

    [Fact]
    public async Task RotateSecret_Returns404WhenNotFound()
    {
        var body = new RotateSecretBody(ScopeType.Machine, null, "new-secret-value");
        _secretMock.Setup(s => s.RotateSecretAsync(It.IsAny<RotateSecretDto>(), "test-user", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Definition not found"));

        var result = await _sut.RotateSecret("missing.key", body, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeleteSecret_Returns204()
    {
        _secretMock.Setup(s => s.DeleteSecretAsync("app.secret.key", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.DeleteSecret("app.secret.key", CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteSecret_Returns404WhenNotFound()
    {
        _secretMock.Setup(s => s.DeleteSecretAsync("missing.key", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Definition not found"));

        var result = await _sut.DeleteSecret("missing.key", CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }
}
