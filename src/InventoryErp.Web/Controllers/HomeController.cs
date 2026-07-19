using System.Diagnostics;
using InventoryErp.Application.Interfaces;
using InventoryErp.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryErp.Web.Controllers;

/// <summary>
/// The dashboard. Kept on <c>Home/Index</c> rather than a separate <c>DashboardController</c>
/// because that route is already the application default (<c>{controller=Home}/{action=Index}</c>)
/// and where login redirects — so it is the landing page with no routing change, and there is no
/// orphaned <c>/Home/Index</c> left behind.
/// </summary>
public class HomeController : Controller
{
    /// <summary>Rows in the reorder work list. Deliberately short — it is a prompt, not a report.</summary>
    private const int LowStockPreviewCount = 5;

    /// <summary>
    /// Only the low-stock preview pages products; the headline figures are database counts with
    /// no such ceiling.
    /// </summary>
    private const int LowStockScanSize = 200;

    private readonly IDashboardService _dashboard;
    private readonly IProductService _products;
    private readonly ICurrentCompanyProvider _currentCompany;

    public HomeController(
        IDashboardService dashboard,
        IProductService products,
        ICurrentCompanyProvider currentCompany)
    {
        _dashboard = dashboard;
        _products = products;
        _currentCompany = currentCompany;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var companyId = await _currentCompany.GetCompanyIdAsync(cancellationToken);

        if (companyId is null)
        {
            return View(new DashboardViewModel { LoadError = "No company has been configured yet." });
        }

        var stats = await _dashboard.GetStatsAsync(companyId.Value, cancellationToken: cancellationToken);

        if (!stats.IsSuccess)
        {
            return View(new DashboardViewModel { LoadError = stats.Error });
        }

        // Separate concern from the KPI counts: this is the reorder queue, and a failure to load
        // it should not blank the whole dashboard.
        var lowStock = await _products.GetAllAsync(
            companyId.Value, 1, LowStockScanSize, cancellationToken);

        var lowStockProducts = lowStock.IsSuccess
            ? lowStock.Data!.Items
                .Where(p => p.IsBelowReorderLevel)
                .OrderBy(p => p.CurrentStock)
                .Take(LowStockPreviewCount)
                .ToList()
            : [];

        return View(new DashboardViewModel
        {
            Stats = stats.Data!,
            LowStockProducts = lowStockProducts,
        });
    }

    public IActionResult Privacy() => View();

    // Anonymous so an unhandled error for a signed-out user shows the error page rather than
    // bouncing to login, which would hide the failure.
    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
