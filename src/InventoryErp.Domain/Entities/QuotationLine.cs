using InventoryErp.Domain.Common;

namespace InventoryErp.Domain.Entities;

/// <summary>
/// A single product line on a <see cref="Quotation"/>. Price and GST rate are copied from the
/// product at the time of quoting, so later changes to the product do not alter issued quotations.
/// </summary>
public class QuotationLine : BaseEntity
{
    public Guid QuotationId { get; set; }

    public Guid ProductId { get; set; }

    public int Quantity { get; set; }

    /// <summary>Price per unit as quoted, copied from the product's selling price.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Line-level discount as a percentage, e.g. 5.0 for 5%.</summary>
    public decimal DiscountPercent { get; set; }

    /// <summary>GST rate as a percentage, copied from the product at the time of quoting.</summary>
    public decimal GstPercent { get; set; }

    public decimal TaxAmount { get; set; }

    /// <summary>Line total after discount and tax.</summary>
    public decimal TotalAmount { get; set; }
}
