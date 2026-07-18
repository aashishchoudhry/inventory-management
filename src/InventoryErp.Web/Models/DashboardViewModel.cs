using InventoryErp.Application.DTOs.Products;

namespace InventoryErp.Web.Models;

public sealed class DashboardViewModel
{
    public int TotalProducts { get; init; }

    public int ActiveProducts { get; init; }

    public int LowStockCount { get; init; }

    /// <summary>Stock on hand valued at selling price. Not a cost-basis valuation.</summary>
    public decimal InventoryValue { get; init; }

    public IReadOnlyList<ProductDto> LowStockProducts { get; init; } = [];

    /// <summary>Set when the product service failed, so the view can say so plainly.</summary>
    public string? LoadError { get; init; }
}
