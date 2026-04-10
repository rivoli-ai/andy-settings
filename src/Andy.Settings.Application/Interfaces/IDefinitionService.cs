using Andy.Settings.Application.DTOs.Common;
using Andy.Settings.Application.DTOs.Definitions;

namespace Andy.Settings.Application.Interfaces;

public interface IDefinitionService
{
    Task<DefinitionDto?> GetAsync(string key, CancellationToken ct = default);
    Task<PagedResult<DefinitionDto>> SearchAsync(DefinitionQuery query, CancellationToken ct = default);
    Task<DefinitionDto> CreateAsync(CreateDefinitionDto dto, CancellationToken ct = default);
    Task<DefinitionDto> UpdateAsync(string key, UpdateDefinitionDto dto, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
}
