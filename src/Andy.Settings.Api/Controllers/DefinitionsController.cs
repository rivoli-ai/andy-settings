using Andy.Settings.Application.DTOs.Common;
using Andy.Settings.Application.DTOs.Definitions;
using Andy.Settings.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Andy.Settings.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DefinitionsController : ControllerBase
{
    private readonly IDefinitionService _service;

    public DefinitionsController(IDefinitionService service) => _service = service;

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<DefinitionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] DefinitionQuery query, CancellationToken ct)
    {
        var result = await _service.SearchAsync(query, ct);
        return Ok(result);
    }

    [HttpGet("{key}")]
    [ProducesResponseType(typeof(DefinitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string key, CancellationToken ct)
    {
        var result = await _service.GetAsync(key, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(DefinitionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateDefinitionDto dto, CancellationToken ct)
    {
        try
        {
            var result = await _service.CreateAsync(dto, ct);
            return CreatedAtAction(nameof(Get), new { key = result.Key }, result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPut("{key}")]
    [ProducesResponseType(typeof(DefinitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(string key, [FromBody] UpdateDefinitionDto dto, CancellationToken ct)
    {
        try
        {
            var result = await _service.UpdateAsync(key, dto, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{key}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string key, CancellationToken ct)
    {
        try
        {
            await _service.DeleteAsync(key, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
