using InventoryErp.Domain.Enums;

namespace InventoryErp.Application.DTOs.Products;

public sealed record ProductDto
{
    public Guid Id { get; init; }
    public string Sku { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public decimal UnitPrice { get; init; }
    public int QuantityOnHand { get; init; }
    public int ReorderLevel { get; init; }
    public ProductStatus Status { get; init; }
    public bool IsBelowReorderLevel { get; init; }
}
