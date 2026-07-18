using System.Diagnostics;
using InventoryErp.Application.Interfaces;
using InventoryErp.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryErp.Web.Controllers;

public class HomeController : Controller
{
    private readonly IProductService _productService;

    public HomeController(IProductService productService)
    {
        _productService = productService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        // Derived from the existing product service rather than a new dashboard service:
        // Customer and Quotation have no application services yet, so there is nothing else
        // to aggregate.
        var result = await _productService.GetAllAsync(cancellationToken);

        if (!result.IsSuccess)
        {
            return View(new DashboardViewModel { LoadError = result.Error });
        }

        var products = result.Data!;

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
