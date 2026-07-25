using Andy.Settings.Application.DTOs.Common;
using Andy.Settings.Application.DTOs.Definitions;

namespace Andy.Settings.Application.Interfaces;

public interface IDefinitionService
{
    Task<DefinitionDto?> GetAsync(string key, CancellationToken ct = default);
    Task<PagedResult<DefinitionDto>> SearchAsync(DefinitionQuery query, CancellationToken ct = default);

    /// <summary>
    /// Every distinct, non-empty category, sorted.
    /// </summary>
    /// <remarks>
    /// Exists so callers do not have to approximate "all categories" by
    /// requesting an oversized page. The MCP <c>settings_categories</c> tool
    /// used to ask for 1000 definitions and reduce them client-side, which
    /// silently under-reported once the catalog outgrew the page — exactly the
    /// silent-truncation failure the paging cap was added to prevent.
    /// </remarks>
    Task<IReadOnlyList<string>> GetCategoriesAsync(string? applicationCode = null, CancellationToken ct = default);
    Task<DefinitionDto> CreateAsync(CreateDefinitionDto dto, string? actorId = null, CancellationToken ct = default);
    Task<DefinitionDto> UpdateAsync(string key, UpdateDefinitionDto dto, string? actorId = null, CancellationToken ct = default);

    /// <summary>
    /// Deletes a definition. This CASCADES to every stored assignment and
    /// every encrypted secret for the key — see the remarks on the
    /// implementation before calling it.
    /// </summary>
    Task DeleteAsync(string key, string? actorId = null, CancellationToken ct = default);
}
