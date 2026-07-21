using Andy.Settings.Application.DTOs.Audit;
using Andy.Settings.Application.DTOs.Common;
using Andy.Settings.Application.Interfaces;
using Andy.Rbac.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Andy.Settings.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AuditController : ControllerBase
{
    private readonly IAuditService _service;

    public AuditController(IAuditService service) => _service = service;

    [HttpGet]
    [RequirePermission("audit:read")]
    [ProducesResponseType(typeof(PagedResult<AuditEventDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Query([FromQuery] AuditQuery query, CancellationToken ct)
    {
        var result = await _service.QueryAsync(query, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("audit:read")]
    [ProducesResponseType(typeof(AuditEventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }
}
