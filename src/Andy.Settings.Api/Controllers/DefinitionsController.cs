using Andy.Settings.Application.DTOs.Common;
using Andy.Settings.Application.DTOs.Definitions;
using Andy.Settings.Application.Interfaces;
using Andy.Rbac.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Andy.Settings.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DefinitionsController : ControllerBase
{
    private readonly IDefinitionService _service;
    private readonly ICurrentUserService _currentUser;

    public DefinitionsController(IDefinitionService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet]
    [RequirePermission("definition:read")]
    [ProducesResponseType(typeof(PagedResult<DefinitionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] DefinitionQuery query, CancellationToken ct)
    {
        var result = await _service.SearchAsync(query, ct);
        return Ok(result);
    }

    [HttpGet("{key}")]
    [RequirePermission("definition:read")]
    [ProducesResponseType(typeof(DefinitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string key, CancellationToken ct)
    {
        var result = await _service.GetAsync(key, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [RequirePermission("definition:write")]
    [ProducesResponseType(typeof(DefinitionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateDefinitionDto dto, CancellationToken ct)
    {
        try
        {
            var result = await _service.CreateAsync(dto, _currentUser.GetUserId(), ct);
            return CreatedAtAction(nameof(Get), new { key = result.Key }, result);
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{key}")]
    [RequirePermission("definition:write")]
    [ProducesResponseType(typeof(DefinitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(string key, [FromBody] UpdateDefinitionDto dto, CancellationToken ct)
    {
        try
        {
            var result = await _service.UpdateAsync(key, dto, _currentUser.GetUserId(), ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{key}")]
    [RequirePermission("definition:delete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string key, CancellationToken ct)
    {
        try
        {
            await _service.DeleteAsync(key, _currentUser.GetUserId(), ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
