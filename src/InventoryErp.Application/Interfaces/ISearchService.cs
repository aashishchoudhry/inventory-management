using InventoryErp.Application.Common;
using InventoryErp.Application.DTOs.Search;

namespace InventoryErp.Application.Interfaces;

public interface ISearchService
{
    /// <summary>Shortest keyword that will be queried.</summary>
    const int MinimumKeywordLength = 2;

    /// <summary>
    /// Searches products (name, SKU, barcode), customers (name, code) and quotations (number)
    /// in one call, returning a combined list.
    /// </summary>
    /// <param name="perTypeLimit">
    /// Caps hits per entity type. Small for a nav dropdown, larger for a full results page.
    /// </param>
    /// <returns>
    /// <c>ValidationFailed</c> when the company id is empty or the keyword is shorter than
    /// <see cref="MinimumKeywordLength"/> after trimming; otherwise <c>Success</c>, possibly
    /// with zero results.
    /// </returns>
    Task<ServiceResult<SearchResultsDto>> SearchAsync(
        Guid companyId,
        string? keyword,
        int perTypeLimit = 5,
        CancellationToken cancellationToken = default);
}
