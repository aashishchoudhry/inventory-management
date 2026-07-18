using InventoryErp.Domain.Common;

namespace InventoryErp.Domain.Entities;

/// <summary>
/// A priced offer issued to a customer. Monetary totals are stored rather than recomputed on
/// read, so a quotation keeps the figures it was issued with even if product prices change later.
/// </summary>
public class Quotation : BaseEntity
{
    /// <summary>Owning tenant. Every query for quotations must filter on this.</summary>
    public Guid CompanyId { get; set; }

    /// <summary>Human-readable reference, unique within the company.</summary>
    public required string QuotationNumber { get; set; }

    public Guid CustomerId { get; set; }

    public DateTime QuotationDate { get; set; }

    /// <summary>Date the offer lapses. Null means it does not expire.</summary>
    public DateTime? ValidUntil { get; set; }

    /// <summary>Sum of line totals before tax and discount.</summary>
    public decimal SubTotal { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal DiscountAmount { get; set; }

    /// <summary>Final payable amount.</summary>
    public decimal TotalAmount { get; set; }

    public string? Notes { get; set; }
}
