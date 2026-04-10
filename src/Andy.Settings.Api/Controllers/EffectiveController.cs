using Andy.Settings.Application.DTOs.Effective;
using Andy.Settings.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Andy.Settings.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EffectiveController : ControllerBase
{
    private readonly IResolutionService _service;

    public EffectiveController(IResolutionService service) => _service = service;

    [HttpPost("resolve")]
    [ProducesResponseType(typeof(ResolvedSetting), StatusCodes.Status200OK)]
    public async Task<IActionResult> Resolve([FromBody] ResolveRequest request, CancellationToken ct)
    {
        var result = await _service.ResolveAsync(request.Key, request.Context, ct);
        return Ok(result);
    }

    [HttpPost("resolve-batch")]
    [ProducesResponseType(typeof(IReadOnlyList<ResolvedSetting>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ResolveBatch([FromBody] ResolveBatchRequest request, CancellationToken ct)
    {
        var results = await _service.ResolveBatchAsync(request.Keys, request.Context, ct);
        return Ok(results);
    }

    [HttpPost("explain")]
    [ProducesResponseType(typeof(ResolvedSetting), StatusCodes.Status200OK)]
    public async Task<IActionResult> Explain([FromBody] ResolveRequest request, CancellationToken ct)
    {
        var result = await _service.ExplainAsync(request.Key, request.Context, ct);
        return Ok(result);
    }
}
