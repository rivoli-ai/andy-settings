using Andy.Settings.Application.DTOs.Common;
using Andy.Settings.Application.DTOs.Values;
using Andy.Settings.Application.Interfaces;
using Andy.Settings.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Andy.Settings.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ValuesController : ControllerBase
{
    private readonly IAssignmentService _service;
    private readonly ICurrentUserService _currentUser;

    public ValuesController(IAssignmentService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AssignmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? definitionKey,
        [FromQuery] ScopeType? scopeType,
        [FromQuery] string? scopeId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var result = await _service.ListByScopeAsync(definitionKey, scopeType, scopeId, page, pageSize, ct);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(AssignmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Set([FromBody] SetValueDto dto, CancellationToken ct)
    {
        try
        {
            var result = await _service.SetAsync(dto, _currentUser.GetUserId(), ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("etag"))
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await _service.DeleteAsync(id, _currentUser.GetUserId(), ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("bulk")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> BulkSet([FromBody] IEnumerable<SetValueDto> dtos, CancellationToken ct)
    {
        await _service.BulkSetAsync(dtos, _currentUser.GetUserId(), ct);
        return NoContent();
    }
}
