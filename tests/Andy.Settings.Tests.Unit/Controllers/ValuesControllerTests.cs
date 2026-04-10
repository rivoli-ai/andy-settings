using Andy.Settings.Api.Controllers;
using Andy.Settings.Application.DTOs.Values;
using Andy.Settings.Application.Interfaces;
using Andy.Settings.Domain.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Andy.Settings.Tests.Unit.Controllers;

public class ValuesControllerTests
{
    private readonly Mock<IAssignmentService> _assignmentMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly ValuesController _sut;

    public ValuesControllerTests()
    {
        _currentUserMock.Setup(u => u.GetUserId()).Returns("test-user");
        _sut = new ValuesController(_assignmentMock.Object, _currentUserMock.Object);
    }

    private static AssignmentDto MakeAssignmentDto() => new(
        Id: Guid.NewGuid(),
        DefinitionId: Guid.NewGuid(),
        DefinitionKey: "app.test.key",
        ScopeType: ScopeType.User,
        ScopeId: "user1",
        ValueJson: "\"test-value\"",
        Etag: "abc123",
        Version: 1,
        UpdatedBy: "test-user",
        CreatedAt: DateTimeOffset.UtcNow,
        UpdatedAt: DateTimeOffset.UtcNow
    );

    [Fact]
    public async Task Set_Returns200()
    {
        var setDto = new SetValueDto
        {
            DefinitionKey = "app.test.key",
            ScopeType = ScopeType.User,
            ScopeId = "user1",
            ValueJson = "\"new-value\""
        };
        var assignmentDto = MakeAssignmentDto();
        _assignmentMock.Setup(s => s.SetAsync(setDto, "test-user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(assignmentDto);

        var result = await _sut.Set(setDto, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
        okResult.Value.Should().Be(assignmentDto);
    }

    [Fact]
    public async Task Set_DefinitionNotFound_Returns404()
    {
        var setDto = new SetValueDto
        {
            DefinitionKey = "missing.key",
            ScopeType = ScopeType.User,
            ScopeId = "user1",
            ValueJson = "\"value\""
        };
        _assignmentMock.Setup(s => s.SetAsync(setDto, "test-user", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException());

        var result = await _sut.Set(setDto, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Set_ConcurrencyConflict_Returns409()
    {
        var setDto = new SetValueDto
        {
            DefinitionKey = "app.test.key",
            ScopeType = ScopeType.User,
            ScopeId = "user1",
            ValueJson = "\"value\"",
            Etag = "stale-etag"
        };
        _assignmentMock.Setup(s => s.SetAsync(setDto, "test-user", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("etag mismatch"));

        var result = await _sut.Set(setDto, CancellationToken.None);

        var conflictResult = result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflictResult.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task Delete_ExistingId_Returns204()
    {
        var id = Guid.NewGuid();
        _assignmentMock.Setup(s => s.DeleteAsync(id, "test-user", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.Delete(id, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Delete_MissingId_Returns404()
    {
        var id = Guid.NewGuid();
        _assignmentMock.Setup(s => s.DeleteAsync(id, "test-user", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException());

        var result = await _sut.Delete(id, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }
}
