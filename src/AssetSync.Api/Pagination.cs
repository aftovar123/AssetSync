using Microsoft.EntityFrameworkCore;

namespace AssetSync.Api;

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public static class Pagination
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    /// <summary>
    /// Runs the query as one page. Out-of-range values are clamped instead of
    /// rejected — page below 1 becomes 1, pageSize is kept between 1 and
    /// <see cref="MaxPageSize"/> — so a client can never ask for the whole
    /// table in one request. The query must already be ordered: without a
    /// stable ORDER BY, Skip/Take can return overlapping or missing rows.
    /// </summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IOrderedQueryable<T> query, int? page, int? pageSize, CancellationToken cancellationToken)
        where T : class
    {
        var currentPage = Math.Max(page ?? 1, 1);
        var size = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((currentPage - 1) * size)
            .Take(size)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return new PagedResult<T>(items, currentPage, size, totalCount);
    }
}
