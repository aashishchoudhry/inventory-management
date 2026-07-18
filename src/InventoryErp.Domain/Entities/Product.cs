using InventoryErp.Domain.Common;
using InventoryErp.Domain.Enums;

namespace InventoryErp.Domain.Entities;

public class Product : BaseEntity
{
    /// <summary>Stock keeping unit. Unique across non-deleted products.</summary>
    public required string Sku { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public decimal UnitPrice { get; set; }

    public int QuantityOnHand { get; set; }

    /// <summary>Stock level at or below which the product should be reordered.</summary>
    public int ReorderLevel { get; set; }

    public ProductStatus Status { get; set; } = ProductStatus.Active;

    public bool IsBelowReorderLevel => QuantityOnHand <= ReorderLevel;
}
