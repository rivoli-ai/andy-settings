using Andy.Settings.Application.DTOs.Secrets;
using Andy.Settings.Application.Interfaces;
using Andy.Settings.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Andy.Settings.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SecretsController : ControllerBase
{
    private readonly ISecretService _service;
    private readonly ICurrentUserService _currentUser;

    public SecretsController(ISecretService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpPost("{definitionKey}")]
    [ProducesResponseType(typeof(SecretMetadataDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetSecret(
        string definitionKey,
        [FromBody] SetSecretBody body,
        CancellationToken ct)
    {
        try
        {
            var dto = new SetSecretDto
            {
                DefinitionKey = definitionKey,
                ScopeType = body.ScopeType,
                ScopeId = body.ScopeId,
                PlaintextValue = body.Value
            };
            var result = await _service.SetSecretAsync(dto, _currentUser.GetUserId(), ct);
            return CreatedAtAction(nameof(GetSecret), new { definitionKey }, result);
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

    [HttpGet("{definitionKey}")]
    [ProducesResponseType(typeof(SecretValueResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSecret(
        string definitionKey,
        [FromQuery] ScopeType scopeType = ScopeType.Machine,
        [FromQuery] string? scopeId = null,
        CancellationToken ct = default)
    {
        try
        {
            var value = await _service.GetSecretAsync(new GetSecretDto
            {
                DefinitionKey = definitionKey,
                ScopeType = scopeType,
                ScopeId = scopeId
            }, ct);

            return value is null
                ? NotFound()
                : Ok(new SecretValueResponse(definitionKey, value));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{definitionKey}/rotate")]
    [ProducesResponseType(typeof(SecretMetadataDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RotateSecret(
        string definitionKey,
        [FromBody] RotateSecretBody body,
        CancellationToken ct)
    {
        try
        {
            var dto = new RotateSecretDto
            {
                DefinitionKey = definitionKey,
                ScopeType = body.ScopeType,
                ScopeId = body.ScopeId,
                NewPlaintextValue = body.NewValue
            };
            var result = await _service.RotateSecretAsync(dto, _currentUser.GetUserId(), ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{definitionKey}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSecret(string definitionKey, CancellationToken ct)
    {
        try
        {
            await _service.DeleteSecretAsync(definitionKey, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}

public record SetSecretBody(ScopeType ScopeType, string? ScopeId, string Value);
public record RotateSecretBody(ScopeType ScopeType, string? ScopeId, string NewValue);
public record SecretValueResponse(string DefinitionKey, string Value);
