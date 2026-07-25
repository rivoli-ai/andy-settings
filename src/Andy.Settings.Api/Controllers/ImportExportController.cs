using Andy.Settings.Application.DTOs.ImportExport;
using Andy.Settings.Application.Interfaces;
using Andy.Rbac.Authorization;
using Microsoft.EntityFrameworkCore;
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
    [RequirePermission("export:read")]
    [ProducesResponseType(typeof(ExportResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Export([FromQuery] ExportOptions options, CancellationToken ct)
    {
        var result = await _service.ExportAsync(options, ct);
        return Ok(result);
    }

    /// <summary>
    /// Maximum import document size. The whole document is buffered and
    /// deserialized into memory, so this is a real memory bound, not a
    /// formality (rivoli-ai/andy-settings#140).
    /// </summary>
    private const long MaxImportBytes = 16 * 1024 * 1024;

    [HttpPost("import")]
    [RequirePermission("import:write")]
    [RequestSizeLimit(MaxImportBytes)]
    [ProducesResponseType(typeof(ImportResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Import(CancellationToken ct)
    {
        try
        {
            var result = await _service.ImportAsync(
                Request.Body,
                new ImportOptions { DryRun = false },
                _currentUser.GetUserId(),
                ct);
            return Ok(result);
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (BadHttpRequestException ex)
        {
            // Thrown by RequestSizeLimit when the body exceeds MaxImportBytes.
            return BadRequest(new { error = $"Import document is too large (limit {MaxImportBytes} bytes)." , detail = ex.Message });
        }
        catch (DbUpdateException ex)
        {
            // Previously fell through as an unhandled 500 with an EF stack
            // trace. Persistence failures are now a deliberate response.
            return Problem(
                title: "Import could not be persisted.",
                detail: ex.InnerException?.Message ?? ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpPost("import/preview")]
    [RequirePermission("import:write")]
    [RequestSizeLimit(MaxImportBytes)]
    [ProducesResponseType(typeof(ImportPreview), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PreviewImport(CancellationToken ct)
    {
        try
        {
            var result = await _service.PreviewImportAsync(Request.Body, ct);
            return Ok(result);
        }
        catch (BadHttpRequestException ex)
        {
            return BadRequest(new { error = $"Import document is too large (limit {MaxImportBytes} bytes).", detail = ex.Message });
        }
    }
}
