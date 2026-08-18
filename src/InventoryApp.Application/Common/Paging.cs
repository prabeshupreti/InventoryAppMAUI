using InventoryApp.Contracts.Common;
using Microsoft.EntityFrameworkCore;

namespace InventoryApp.Application.Common;

/// <summary>Server-side paging helpers shared by every list endpoint.</summary>
public static class Paging
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 200;

    public static (int Page, int PageSize) Normalize(PageRequest? request)
    {
        var page = request?.Page ?? 1;
        var size = request?.PageSize ?? DefaultPageSize;

        if (page < 1) page = 1;
        if (size <= 0) size = DefaultPageSize;
        if (size > MaxPageSize) size = MaxPageSize;

        return (page, size);
    }

    public static string SearchTerm(PageRequest? request) =>
        (request?.Search ?? string.Empty).Trim();

    /// <summary>Executes count + page query and returns the materialised page plus metadata.</summary>
    public static async Task<(List<T> Items, PageInfo Info)> ToPageAsync<T>(
        IQueryable<T> query,
        PageRequest? request,
        CancellationToken cancellationToken)
    {
        var (page, size) = Normalize(request);

        var total = await query.CountAsync(cancellationToken);
        var totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)size);

        // Clamp to the last page if the client asked for one past the end.
        if (totalPages > 0 && page > totalPages)
        {
            page = totalPages;
        }

        var items = await query
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return (items, new PageInfo
        {
            Page = page,
            PageSize = size,
            TotalCount = total,
            TotalPages = totalPages
        });
    }

    /// <summary>Applies a sort chosen from a whitelist of allowed key selectors.</summary>
    public static IQueryable<T> ApplySort<T>(
        IQueryable<T> query,
        PageRequest? request,
        IReadOnlyDictionary<string, System.Linq.Expressions.Expression<Func<T, object>>> map,
        string defaultKey)
    {
        var key = (request?.SortBy ?? string.Empty).Trim();
        if (key.Length == 0 || !map.ContainsKey(key))
        {
            key = defaultKey;
        }

        var selector = map[key];
        return request?.SortDescending == true
            ? query.OrderByDescending(selector)
            : query.OrderBy(selector);
    }
}
