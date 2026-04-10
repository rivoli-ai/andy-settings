using Andy.Settings.Application.DTOs.ImportExport;
using Andy.Settings.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Andy.Settings.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class ImportExportController : ControllerBase
{
    private readonly IExportImportService _service;
    private readonly ICurrentUserService _currentUser;

    public ImportExportController(IExportImportService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet("export")]
    [ProducesResponseType(typeof(ExportResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Export([FromQuery] ExportOptions options, CancellationToken ct)
    {
        var result = await _service.ExportAsync(options, ct);
        return Ok(result);
    }

    [HttpPost("import")]
    [ProducesResponseType(typeof(ImportResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Import(CancellationToken ct)
    {
        var result = await _service.ImportAsync(
            Request.Body,
            new ImportOptions { DryRun = false },
            _currentUser.GetUserId(),
            ct);
        return Ok(result);
    }

    [HttpPost("import/preview")]
    [ProducesResponseType(typeof(ImportPreview), StatusCodes.Status200OK)]
    public async Task<IActionResult> PreviewImport(CancellationToken ct)
    {
        var result = await _service.PreviewImportAsync(Request.Body, ct);
        return Ok(result);
    }
}
