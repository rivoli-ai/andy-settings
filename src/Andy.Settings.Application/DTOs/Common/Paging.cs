namespace Andy.Settings.Application.DTOs.Common;

/// <summary>
/// Single place where page/pageSize inputs are made safe before they reach a
/// query.
/// </summary>
/// <remarks>
/// Every list repository previously built <c>Skip((page - 1) * pageSize)</c>
/// straight from unvalidated query-string input, with no lower bound on
/// <c>page</c> and no upper bound on <c>pageSize</c>
/// (rivoli-ai/andy-settings#134). That produced a negative SQL <c>OFFSET</c>,
/// which the two supported providers disagree about: SQLite clamps it and
/// silently serves page 1, PostgreSQL raises
/// <c>OFFSET must not be negative</c> and the request becomes a 500. The same
/// request therefore behaved differently in the Conductor-embedded deployment
/// and the shared one. An uncapped <c>pageSize</c> also let a single request
/// pull an entire table.
/// </remarks>
public static class Paging
{
    /// <summary>Default page size when a caller does not specify one.</summary>
    public const int DefaultPageSize = 25;

    /// <summary>
    /// Upper bound on a single page. Callers asking for more get this many;
    /// the response's <see cref="PagedResult{T}.TotalCount"/> still reports the
    /// full count, so a client can tell it needs to keep paging.
    /// </summary>
    public const int MaxPageSize = 500;

    /// <summary>
    /// Clamps a (page, pageSize) pair into the supported range. Clamping
    /// rather than throwing keeps existing clients working; the bounds are
    /// documented on the API surface.
    /// </summary>
    public static (int Page, int PageSize) Normalize(int page, int pageSize)
    {
        var normalizedPage = page < 1 ? 1 : page;
        var normalizedPageSize = pageSize < 1
            ? DefaultPageSize
            : pageSize > MaxPageSize ? MaxPageSize : pageSize;

        return (normalizedPage, normalizedPageSize);
    }
}
