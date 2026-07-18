using System.Globalization;
using InventoryErp.Application.Common;
using InventoryErp.Application.DTOs.Search;
using InventoryErp.Application.Interfaces;

namespace InventoryErp.Application.Services;

/// <summary>
/// Global search across products, customers and quotations.
/// </summary>
/// <remarks>
/// Composes the existing entity services rather than querying repositories directly, so each
/// entity's matching rules and tenant scoping stay defined in exactly one place. Adding a
/// searchable field to products, for example, changes <c>ProductService</c> alone and global
/// search inherits it.
/// </remarks>
public sealed class SearchService : ISearchService
{
    private const int MaxPerTypeLimit = 50;

    private static readonly CultureInfo Money = CultureInfo.GetCultureInfo("en-IN");

    private readonly IProductService _products;
    private readonly ICustomerService _customers;
    private readonly IQuotationService _quotations;

    public SearchService(
        IProductService products,
        ICustomerService customers,
        IQuotationService quotations)
    {
        _products = products;
        _customers = customers;
        _quotations = quotations;
    }

    public async Task<ServiceResult<SearchResultsDto>> SearchAsync(
        Guid companyId,
        string? keyword,
        int perTypeLimit = 5,
        CancellationToken cancellationToken = default)
    {
        var term = keyword?.Trim() ?? string.Empty;
        var errors = new List<string>();

        if (companyId == Guid.Empty)
        {
            errors.Add("A company must be specified.");
        }

        // Enforced here, not only in the browser: the endpoint is reachable directly, and an
        // unbounded single-character search would scan every table.
        if (term.Length < ISearchService.MinimumKeywordLength)
        {
            errors.Add($"Enter at least {ISearchService.MinimumKeywordLength} characters to search.");
        }

        if (perTypeLimit is < 1 or > MaxPerTypeLimit)
        {
            errors.Add($"Result limit must be between 1 and {MaxPerTypeLimit}.");
        }

        if (errors.Count > 0)
        {
            return ServiceResult<SearchResultsDto>.Invalid(errors);
        }

        // Sequential rather than concurrent: these services share one scoped DbContext, which is
        // not thread-safe. Running them with Task.WhenAll would intermittently throw.
        var products = await _products.SearchAsync(companyId, term, 1, perTypeLimit, cancellationToken);
        var customers = await _customers.SearchAsync(companyId, term, 1, perTypeLimit, cancellationToken);
        var quotations = await _quotations.SearchAsync(companyId, term, 1, perTypeLimit, cancellationToken);

        if (!products.IsSuccess || !customers.IsSuccess || !quotations.IsSuccess)
        {
            return ServiceResult<SearchResultsDto>.Failure("Search could not be completed.");
        }

        var items = new List<SearchResultDto>();

        items.AddRange(products.Data!.Items.Select(p => new SearchResultDto
        {
            Type = SearchResultType.Product,
            Id = p.Id,
            Title = p.Name,
            Subtitle = p.Sku,
            Meta = p.SellingPrice.ToString("N2", Money),
        }));

        items.AddRange(customers.Data!.Items.Select(c => new SearchResultDto
        {
            Type = SearchResultType.Customer,
            Id = c.Id,
            Title = c.Name,
            Subtitle = c.Code,
            Meta = c.City,
        }));

        items.AddRange(quotations.Data!.Items.Select(q => new SearchResultDto
        {
            Type = SearchResultType.Quotation,
            Id = q.Id,
            Title = q.QuotationNumber,
            Subtitle = q.CustomerName,
            Meta = q.TotalAmount.ToString("N2", Money),
        }));

        // TotalCount from each page is the full match count, so this detects a cap even though
        // only perTypeLimit rows were fetched.
        var truncated = products.Data.TotalCount > perTypeLimit
            || customers.Data.TotalCount > perTypeLimit
            || quotations.Data.TotalCount > perTypeLimit;

        return ServiceResult<SearchResultsDto>.Success(new SearchResultsDto
        {
            Keyword = term,
            Items = items,
            ProductCount = products.Data.TotalCount,
            CustomerCount = customers.Data.TotalCount,
            QuotationCount = quotations.Data.TotalCount,
            IsTruncated = truncated,
        });
    }
}
