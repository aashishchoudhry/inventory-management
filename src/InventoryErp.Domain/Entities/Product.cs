using InventoryErp.Domain.Common;
using InventoryErp.Domain.Enums;

namespace InventoryErp.Domain.Entities;

/// <summary>
/// A sellable item. Scoped to a <see cref="Company"/> for tenant isolation.
/// </summary>
public class Product : BaseEntity
{
    /// <summary>Owning tenant. Every query for products must filter on this.</summary>
    public Guid CompanyId { get; set; }

    public required string Name { get; set; }

    /// <summary>Stock keeping unit. Unique within the company across non-deleted products.</summary>
    public required string Sku { get; set; }

    /// <summary>Scanned barcode (EAN/UPC). Optional; not every item carries one.</summary>
    public string? Barcode { get; set; }

    public string? Description { get; set; }

    public decimal SellingPrice { get; set; }

    /// <summary>GST rate as a percentage, e.g. 18.0 for 18%.</summary>
    public decimal GstPercent { get; set; }

    public int CurrentStock { get; set; }

    /// <summary>Stock level at or below which the product should be reordered.</summary>
    public int ReorderLevel { get; set; }

    public ProductStatus Status { get; set; } = ProductStatus.Active;

    public bool IsBelowReorderLevel => CurrentStock <= ReorderLevel;
}
