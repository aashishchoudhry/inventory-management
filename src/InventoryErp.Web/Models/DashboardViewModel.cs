using InventoryErp.Application.DTOs.Dashboard;
using InventoryErp.Application.DTOs.Products;

namespace InventoryErp.Web.Models;

public sealed class DashboardViewModel
{
    public DashboardStatsDto Stats { get; init; } = new();

    /// <summary>
    /// Work list under the KPI cards. Not a KPI — it is the follow-up queue for the stock the
    /// business needs to reorder.
    /// </summary>
    public IReadOnlyList<ProductDto> LowStockProducts { get; init; } = [];

    /// <summary>Set when a service failed, so the view can say so plainly.</summary>
    public string? LoadError { get; init; }
}
