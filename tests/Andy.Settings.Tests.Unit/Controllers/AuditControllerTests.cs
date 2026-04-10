using Andy.Settings.Api.Controllers;
using Andy.Settings.Application.DTOs.Audit;
using Andy.Settings.Application.DTOs.Common;
using Andy.Settings.Application.Interfaces;
using Andy.Settings.Domain.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Andy.Settings.Tests.Unit.Controllers;

public class AuditControllerTests
{
    private readonly Mock<IAuditService> _serviceMock = new();
    private readonly AuditController _sut;

    public AuditControllerTests()
    {
        _sut = new AuditController(_serviceMock.Object);
    }

    private static AuditEventDto MakeAuditEventDto() => new(
        Id: Guid.NewGuid(),
        EventType: AuditEventType.Created,
        DefinitionKey: "app.test.key",
        ScopeType: ScopeType.Machine,
        ScopeId: null,
        ActorType: "User",
        ActorId: "test-user",
        BeforeJson: null,
        AfterJson: "\"value\"",
        CorrelationId: null,
        CreatedAt: DateTimeOffset.UtcNow
    );

    [Fact]
    public async Task Query_Returns200WithPagedResult()
    {
        var events = new List<AuditEventDto> { MakeAuditEventDto() };
        var paged = new PagedResult<AuditEventDto>(events, 1, 1, 25);
        _serviceMock.Setup(s => s.QueryAsync(It.IsAny<AuditQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paged);

        var result = await _sut.Query(new AuditQuery(), CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
        okResult.Value.Should().Be(paged);
    }

    [Fact]
    public async Task GetById_Returns200WhenFound()
    {
        var id = Guid.NewGuid();
        var dto = MakeAuditEventDto();
        _serviceMock.Setup(s => s.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await _sut.GetById(id, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
        okResult.Value.Should().Be(dto);
    }

    [Fact]
    public async Task GetById_Returns404WhenNotFound()
    {
        var id = Guid.NewGuid();
        _serviceMock.Setup(s => s.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuditEventDto?)null);

        var result = await _sut.GetById(id, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }
}
