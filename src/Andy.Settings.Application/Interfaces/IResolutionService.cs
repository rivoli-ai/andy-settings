using Andy.Settings.Application.DTOs.Effective;

namespace Andy.Settings.Application.Interfaces;

public interface IResolutionService
{
    Task<ResolvedSetting> ResolveAsync(string key, ResolutionContext context, CancellationToken ct = default);
    Task<IReadOnlyList<ResolvedSetting>> ResolveBatchAsync(IEnumerable<string> keys, ResolutionContext context, CancellationToken ct = default);
    Task<ResolvedSetting> ExplainAsync(string key, ResolutionContext context, CancellationToken ct = default);
}
