using InventoryErp.Application.DTOs.Search;

namespace InventoryErp.Web.Models;

public sealed class SearchViewModel
{
    public string Keyword { get; init; } = string.Empty;

    public SearchResultsDto? Results { get; init; }

    /// <summary>Set when the keyword was too short or the search failed.</summary>
    public string? Message { get; init; }

    /// <summary>True when the user arrived with no keyword at all, rather than a rejected one.</summary>
    public bool IsEmptyQuery => string.IsNullOrWhiteSpace(Keyword);
}

/// <summary>
/// Maps a search hit onto its route. Lives in the web layer because route shapes are a web
/// concern — the Application layer returns a type and an id, never a URL.
/// </summary>
public static class SearchResultRoutes
{
    public static (string Controller, string Action) For(SearchResultType type) => type switch
    {
        SearchResultType.Product => ("Products", "Details"),
        SearchResultType.Customer => ("Customers", "Index"),
        SearchResultType.Quotation => ("Quotations", "Details"),
        _ => ("Home", "Index"),
    };

    /// <summary>
    /// Customers have no detail screen yet, so a customer hit lands on the list rather than a
    /// per-record page. Kept explicit so it is obvious this is a gap, not a design choice.
    /// </summary>
    public static bool HasDetailPage(SearchResultType type) => type != SearchResultType.Customer;

    public static string Label(SearchResultType type) => type switch
    {
        SearchResultType.Product => "Product",
        SearchResultType.Customer => "Customer",
        SearchResultType.Quotation => "Quotation",
        _ => "Item",
    };

    public static string Icon(SearchResultType type) => type switch
    {
        SearchResultType.Product => "i-box",
        SearchResultType.Customer => "i-users",
        SearchResultType.Quotation => "i-file",
        _ => "i-search",
    };
}
