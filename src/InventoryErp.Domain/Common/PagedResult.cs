namespace InventoryErp.Domain.Common;

/// <summary>
/// A single page of results plus the total count of matching rows. Lives in Domain because the
/// repository contract returns it; it is a plain generic container with no dependencies.
/// </summary>
public sealed class PagedResult<T>
{
    public PagedResult(IReadOnlyList<T> items, int totalCount, int pageNumber, int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        PageNumber = pageNumber;
        PageSize = pageSize;
    }

    public IReadOnlyList<T> Items { get; }

    /// <summary>Total matching rows across all pages, not just this page.</summary>
    public int TotalCount { get; }

    public int PageNumber { get; }

    public int PageSize { get; }

    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPreviousPage => PageNumber > 1;

    public bool HasNextPage => PageNumber < TotalPages;

    /// <summary>Projects the items while preserving the paging metadata — used to map entities to DTOs.</summary>
    public PagedResult<TOut> Map<TOut>(Func<T, TOut> selector)
        => new(Items.Select(selector).ToList(), TotalCount, PageNumber, PageSize);

    public static PagedResult<T> Empty(int pageNumber, int pageSize)
        => new([], 0, pageNumber, pageSize);
}
