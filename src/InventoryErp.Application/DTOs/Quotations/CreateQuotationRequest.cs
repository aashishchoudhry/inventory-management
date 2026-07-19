namespace InventoryErp.Application.DTOs.Quotations;

/// <summary>
/// Input for creating a quotation. Monetary totals are deliberately absent — they are computed
/// server-side from the lines, never accepted from the caller.
/// </summary>
public sealed class CreateQuotationRequest
{
    // No CompanyId: the owning tenant is an explicit service argument resolved from the
    // signed-in user, never bound from the request. See CreateProductRequest for the reasoning.

    public Guid CustomerId { get; set; }

    public DateTime QuotationDate { get; set; }

    /// <summary>Null means the quotation does not expire.</summary>
    public DateTime? ValidUntil { get; set; }

    public string? Notes { get; set; }

    public IReadOnlyList<CreateQuotationLineRequest> Lines { get; set; } = [];
}

/// <summary>
/// One requested line. <see cref="UnitPrice"/> and <see cref="GstPercent"/> are supplied by the
/// caller rather than read from the product, so a quotation can be issued at a negotiated price
/// and keeps the figures it was issued with.
/// </summary>
public sealed class CreateQuotationLineRequest
{
    public Guid ProductId { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    /// <summary>Line discount as a percentage, e.g. 5 for 5%.</summary>
    public decimal DiscountPercent { get; set; }

    /// <summary>GST rate as a percentage, e.g. 18 for 18%.</summary>
    public decimal GstPercent { get; set; }
}
