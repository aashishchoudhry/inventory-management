namespace InventoryErp.Application.DTOs.Search;

public enum SearchResultType
{
    Product = 0,
    Customer = 1,
    Quotation = 2,
}

/// <summary>
/// One hit from a global search, flattened to a shape the UI can render uniformly regardless of
/// which entity it came from.
/// </summary>
/// <remarks>
/// Deliberately carries no URL. Route shapes are a web-layer concern, so the caller maps
/// <see cref="Type"/> and <see cref="Id"/> onto a link — keeping Application free of routing.
/// </remarks>
public sealed record SearchResultDto
{
    public SearchResultType Type { get; init; }

    public Guid Id { get; init; }

    /// <summary>Primary line — product or customer name, or the quotation number.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Secondary line — SKU, customer code, or the quotation's customer.</summary>
    public string? Subtitle { get; init; }

    /// <summary>Right-aligned detail — price, city, or total.</summary>
    public string? Meta { get; init; }
}

public sealed record SearchResultsDto
{
    public string Keyword { get; init; } = string.Empty;

    public IReadOnlyList<SearchResultDto> Items { get; init; } = [];

    public int ProductCount { get; init; }
    public int CustomerCount { get; init; }
    public int QuotationCount { get; init; }

    public int TotalCount => ProductCount + CustomerCount + QuotationCount;

    /// <summary>True when a per-type cap trimmed the results, so the UI can say "showing first N".</summary>
    public bool IsTruncated { get; init; }
}
