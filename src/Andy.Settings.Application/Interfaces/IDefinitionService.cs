using Andy.Settings.Application.DTOs.Common;
using Andy.Settings.Application.DTOs.Definitions;

namespace Andy.Settings.Application.Interfaces;

public interface IDefinitionService
{
    Task<DefinitionDto?> GetAsync(string key, CancellationToken ct = default);
    Task<PagedResult<DefinitionDto>> SearchAsync(DefinitionQuery query, CancellationToken ct = default);
    Task<DefinitionDto> CreateAsync(CreateDefinitionDto dto, string? actorId = null, CancellationToken ct = default);
    Task<DefinitionDto> UpdateAsync(string key, UpdateDefinitionDto dto, string? actorId = null, CancellationToken ct = default);

    /// <summary>
    /// Deletes a definition. This CASCADES to every stored assignment and
    /// every encrypted secret for the key — see the remarks on the
    /// implementation before calling it.
    /// </summary>
    Task DeleteAsync(string key, string? actorId = null, CancellationToken ct = default);
}
