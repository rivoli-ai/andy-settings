using Andy.Settings.Api.Controllers;
using Andy.Settings.Application.DTOs.Common;
using Andy.Settings.Application.DTOs.Values;
using Andy.Settings.Application.Interfaces;
using Andy.Settings.Domain.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Andy.Settings.Tests.Unit.Controllers;

public class ValuesControllerTests_Extended
{
    private readonly Mock<IAssignmentService> _assignmentMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly ValuesController _sut;

    public ValuesControllerTests_Extended()
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
    public async Task BulkSet_Returns204()
    {
        var dtos = new List<SetValueDto>
        {
            new()
            {
                DefinitionKey = "app.key1",
                ScopeType = ScopeType.Machine,
                ValueJson = "\"value1\""
            },
            new()
            {
                DefinitionKey = "app.key2",
                ScopeType = ScopeType.Machine,
                ValueJson = "\"value2\""
            }
        };
        _assignmentMock.Setup(s => s.BulkSetAsync(dtos, "test-user", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.BulkSet(dtos, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task List_Returns200WithPagedResult()
    {
        var assignments = new List<AssignmentDto> { MakeAssignmentDto() };
        var paged = new PagedResult<AssignmentDto>(assignments, 1, 1, 25);
        _assignmentMock.Setup(s => s.ListByScopeAsync(
                It.IsAny<string?>(),
                It.IsAny<ScopeType?>(),
                It.IsAny<string?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(paged);

        var result = await _sut.List(null, null, null, 1, 25, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
        okResult.Value.Should().Be(paged);
    }
}
