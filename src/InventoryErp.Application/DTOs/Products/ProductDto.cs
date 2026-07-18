using InventoryErp.Domain.Enums;

namespace InventoryErp.Application.DTOs.Products;

public sealed record ProductDto
{
    public Guid Id { get; init; }
    public Guid CompanyId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Sku { get; init; } = string.Empty;
    public string? Barcode { get; init; }
    public string? Description { get; init; }
    public decimal SellingPrice { get; init; }
    public decimal GstPercent { get; init; }
    public int CurrentStock { get; init; }
    public int ReorderLevel { get; init; }
    public ProductStatus Status { get; init; }
    public bool IsBelowReorderLevel { get; init; }
}
