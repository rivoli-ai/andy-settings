using Andy.Settings.Application.DTOs.Secrets;
using Andy.Settings.Application.Interfaces;
using Andy.Settings.Domain.Enums;
using Andy.Rbac.Authorization;
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
    [RequirePermission("secret:write")]
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
    [RequirePermission("secret:read")]
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
            }, _currentUser.GetUserId(), ct);

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
    [RequirePermission("secret:write")]
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
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Deletes a secret at a scope, or — with <c>allScopes=true</c> — every
    /// stored secret for the definition.
    /// </summary>
    /// <remarks>
    /// BREAKING CHANGE (rivoli-ai/andy-settings#138). This endpoint used to
    /// take no scope parameters and always delete EVERY scope, so clearing one
    /// user's credential also wiped the Machine-scope value and every other
    /// user's — while Set and Rotate were per-scope.
    ///
    /// A bare <c>DELETE</c> is now rejected rather than silently reinterpreted
    /// as a narrower delete. Quietly switching it to Machine-scope would leave
    /// callers believing they had removed a secret that is still stored, which
    /// is worse for a secrets API than a loud 400.
    /// </remarks>
    [HttpDelete("{definitionKey}")]
    [RequirePermission("secret:write")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSecret(
        string definitionKey,
        [FromQuery] ScopeType? scopeType = null,
        [FromQuery] string? scopeId = null,
        [FromQuery] bool allScopes = false,
        CancellationToken ct = default)
    {
        if (!scopeType.HasValue && !allScopes)
        {
            return BadRequest(new
            {
                error = "Specify the scope to delete (scopeType, plus scopeId for non-Machine scopes), "
                      + "or pass allScopes=true to delete every stored secret for this definition."
            });
        }

        if (scopeType.HasValue && allScopes)
        {
            return BadRequest(new { error = "Pass either a scopeType or allScopes=true, not both." });
        }

        try
        {
            var deleted = await _service.DeleteSecretAsync(
                definitionKey, scopeType, scopeId, _currentUser.GetUserId(), ct);

            return deleted == 0 ? NotFound() : NoContent();
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
