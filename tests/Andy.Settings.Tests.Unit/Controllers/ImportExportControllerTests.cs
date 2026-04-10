using System.Text;
using Andy.Settings.Api.Controllers;
using Andy.Settings.Application.DTOs.ImportExport;
using Andy.Settings.Application.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Andy.Settings.Tests.Unit.Controllers;

public class ImportExportControllerTests
{
    private readonly Mock<IExportImportService> _serviceMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly ImportExportController _sut;

    public ImportExportControllerTests()
    {
        _sut = new ImportExportController(_serviceMock.Object, _currentUserMock.Object);
    }

    private ImportExportController WithRequestBody(string body)
    {
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        _sut.HttpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        return _sut;
    }

    [Fact]
    public async Task Export_ReturnsOk()
    {
        var exportResult = new ExportResult
        {
            Format = "json",
            ExportedAt = DateTimeOffset.UtcNow,
            DefinitionCount = 5,
            AssignmentCount = 3,
            Data = "{}"
        };
        _serviceMock
            .Setup(s => s.ExportAsync(It.IsAny<ExportOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(exportResult);

        var result = await _sut.Export(new ExportOptions(), CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
        okResult.Value.Should().Be(exportResult);
    }

    [Fact]
    public async Task Import_ReturnsOk()
    {
        var importResult = new ImportResult
        {
            DefinitionsCreated = 2,
            DefinitionsUpdated = 1,
            AssignmentsCreated = 3,
            AssignmentsUpdated = 0,
            Warnings = []
        };
        _currentUserMock.Setup(u => u.GetUserId()).Returns("test-user");
        _serviceMock
            .Setup(s => s.ImportAsync(
                It.IsAny<Stream>(),
                It.IsAny<ImportOptions>(),
                "test-user",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(importResult);

        WithRequestBody("{}");

        var result = await _sut.Import(CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
        okResult.Value.Should().Be(importResult);
    }

    [Fact]
    public async Task PreviewImport_ReturnsOk()
    {
        var preview = new ImportPreview
        {
            Additions = [new ImportChange("app.new.key", "Add", null, "\"value\"")],
            Modifications = [],
            Deletions = [],
            ValidationErrors = []
        };
        _serviceMock
            .Setup(s => s.PreviewImportAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(preview);

        WithRequestBody("{}");

        var result = await _sut.PreviewImport(CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
        okResult.Value.Should().Be(preview);
    }
}
