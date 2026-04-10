using Andy.Settings.Api.Controllers;
using Andy.Settings.Application.DTOs.Common;
using Andy.Settings.Application.DTOs.Definitions;
using Andy.Settings.Application.Interfaces;
using Andy.Settings.Domain.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Andy.Settings.Tests.Unit.Controllers;

public class DefinitionsControllerTests
{
    private readonly Mock<IDefinitionService> _serviceMock = new();
    private readonly DefinitionsController _sut;

    public DefinitionsControllerTests()
    {
        _sut = new DefinitionsController(_serviceMock.Object);
    }

    private static DefinitionDto MakeDefinitionDto(string key = "app.test.key") => new(
        Id: Guid.NewGuid(),
        Key: key,
        ApplicationCode: "testapp",
        DisplayName: "Test Setting",
        Description: "A test setting",
        Category: "General",
        DataType: SettingDataType.String,
        DefaultValueJson: "\"default\"",
        ValidationJson: null,
        UiSchemaJson: null,
        IsSecret: false,
        AllowedScopesJson: null,
        TagsJson: null,
        IsDeprecated: false,
        CreatedAt: DateTimeOffset.UtcNow,
        UpdatedAt: DateTimeOffset.UtcNow,
        AssignmentCount: 0
    );

    [Fact]
    public async Task List_Returns200WithPagedResult()
    {
        var dtos = new List<DefinitionDto> { MakeDefinitionDto() };
        var paged = new PagedResult<DefinitionDto>(dtos, 1, 1, 25);
        _serviceMock.Setup(s => s.SearchAsync(It.IsAny<DefinitionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paged);

        var result = await _sut.List(new DefinitionQuery(), CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
        okResult.Value.Should().Be(paged);
    }

    [Fact]
    public async Task Get_ExistingKey_Returns200()
    {
        var dto = MakeDefinitionDto();
        _serviceMock.Setup(s => s.GetAsync("app.test.key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await _sut.Get("app.test.key", CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
        okResult.Value.Should().Be(dto);
    }

    [Fact]
    public async Task Get_MissingKey_Returns404()
    {
        _serviceMock.Setup(s => s.GetAsync("missing.key", It.IsAny<CancellationToken>()))
            .ReturnsAsync((DefinitionDto?)null);

        var result = await _sut.Get("missing.key", CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Create_Returns201()
    {
        var createDto = new CreateDefinitionDto
        {
            Key = "app.new.key",
            ApplicationCode = "testapp",
            DisplayName = "New Setting",
            DataType = SettingDataType.String
        };
        var dto = MakeDefinitionDto("app.new.key");
        _serviceMock.Setup(s => s.CreateAsync(createDto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await _sut.Create(createDto, CancellationToken.None);

        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.StatusCode.Should().Be(201);
        createdResult.Value.Should().Be(dto);
    }

    [Fact]
    public async Task Create_Duplicate_Returns409()
    {
        var createDto = new CreateDefinitionDto
        {
            Key = "app.existing.key",
            ApplicationCode = "testapp",
            DisplayName = "Existing",
            DataType = SettingDataType.String
        };
        _serviceMock.Setup(s => s.CreateAsync(createDto, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Duplicate key"));

        var result = await _sut.Create(createDto, CancellationToken.None);

        var conflictResult = result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflictResult.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task Update_ExistingKey_Returns200()
    {
        var updateDto = new UpdateDefinitionDto { DisplayName = "Updated" };
        var dto = MakeDefinitionDto();
        _serviceMock.Setup(s => s.UpdateAsync("app.test.key", updateDto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await _sut.Update("app.test.key", updateDto, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task Update_MissingKey_Returns404()
    {
        var updateDto = new UpdateDefinitionDto { DisplayName = "Updated" };
        _serviceMock.Setup(s => s.UpdateAsync("missing.key", updateDto, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException());

        var result = await _sut.Update("missing.key", updateDto, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Delete_ExistingKey_Returns204()
    {
        _serviceMock.Setup(s => s.DeleteAsync("app.test.key", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.Delete("app.test.key", CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Delete_MissingKey_Returns404()
    {
        _serviceMock.Setup(s => s.DeleteAsync("missing.key", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException());

        var result = await _sut.Delete("missing.key", CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }
}
