using System.Diagnostics;
using InventoryErp.Application.Interfaces;
using InventoryErp.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryErp.Web.Controllers;

public class HomeController : Controller
{
    /// <summary>
    /// The dashboard aggregates in memory over one page of products. This is the service's maximum
    /// page size, so figures are correct up to 200 products and understate beyond that. Replacing
    /// this needs dedicated aggregate queries rather than a larger page.
    /// </summary>
    private const int StatsPageSize = 200;

    private readonly IProductService _productService;
    private readonly ICurrentCompanyProvider _currentCompany;

    public HomeController(IProductService productService, ICurrentCompanyProvider currentCompany)
    {
        _productService = productService;
        _currentCompany = currentCompany;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var companyId = await _currentCompany.GetCompanyIdAsync(cancellationToken);

        if (companyId is null)
        {
            return View(new DashboardViewModel
            {
                LoadError = "No company has been configured yet.",
            });
        }

        // Derived from the existing product service rather than a new dashboard service:
        // Customer and Quotation have no application services yet, so there is nothing else
        // to aggregate.
        var result = await _productService.GetAllAsync(
            companyId.Value,
            pageNumber: 1,
            pageSize: StatsPageSize,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return View(new DashboardViewModel { LoadError = result.Error });
        }

        var products = result.Data!.Items;

        return View(new DashboardViewModel
        {
            TotalProducts = products.Count,
            ActiveProducts = products.Count(p => p.Status == Domain.Enums.ProductStatus.Active),
            LowStockCount = products.Count(p => p.IsBelowReorderLevel),
            InventoryValue = products.Sum(p => p.SellingPrice * p.CurrentStock),
            LowStockProducts = products
                .Where(p => p.IsBelowReorderLevel)
                .OrderBy(p => p.CurrentStock)
                .Take(5)
                .ToList(),
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
