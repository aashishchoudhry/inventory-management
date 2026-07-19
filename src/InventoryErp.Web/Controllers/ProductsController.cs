using InventoryErp.Application.Common;
using InventoryErp.Application.DTOs.Products;
using InventoryErp.Application.Interfaces;
using InventoryErp.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryErp.Web.Controllers;

[Authorize]
public class ProductsController : Controller
{
    private const int DefaultPageSize = 20;

    private readonly IProductService _productService;
    private readonly ICurrentCompanyProvider _currentCompany;

    public ProductsController(IProductService productService, ICurrentCompanyProvider currentCompany)
    {
        _productService = productService;
        _currentCompany = currentCompany;
    }

    /// <summary>
    /// Lists products for the current company, optionally filtered by <paramref name="q"/>.
    /// Search and listing share one action so the URL is shareable and the browser back button
    /// behaves — a search is just <c>/Products?q=term</c>.
    /// </summary>
    public async Task<IActionResult> Index(
        string? q,
        int page = 1,
        int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        var companyId = await _currentCompany.GetCompanyIdAsync(cancellationToken);

        if (companyId is null)
        {
            return View(new ProductListViewModel
            {
                Keyword = q,
                LoadError = "No company has been configured yet, so there are no products to show.",
            });
        }

        // Out-of-range values are rejected by the service rather than silently clamped here,
        // so the user sees why instead of getting unexpected results.
        var result = await _productService.SearchAsync(
            companyId.Value,
            q,
            page,
            pageSize ?? DefaultPageSize,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return View(new ProductListViewModel
            {
                Keyword = q,
                LoadError = result.ValidationErrors.Count > 0
                    ? string.Join(" ", result.ValidationErrors)
                    : result.Error,
            });
        }

        return View(new ProductListViewModel { Page = result.Data!, Keyword = q });
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var companyId = await _currentCompany.GetCompanyIdAsync(cancellationToken);

        if (companyId is null)
        {
            return NotFound();
        }

        var result = await _productService.GetByIdAsync(id, companyId.Value, cancellationToken);

        return result.IsSuccess ? View(result.Data) : Failed(result);
    }

    public IActionResult Create() => View(new CreateProductRequest());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateProductRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(request);
        }

        // The tenant comes from the signed-in user, never from the posted form.
        var companyId = await _currentCompany.GetCompanyIdAsync(cancellationToken);

        if (companyId is null)
        {
            ModelState.AddModelError(string.Empty, "No company has been configured yet.");
            return View(request);
        }

        var result = await _productService.CreateAsync(companyId.Value, request, cancellationToken);

        if (result.IsSuccess)
        {
            TempData["Success"] = $"Product '{result.Data!.Name}' was created.";
            return RedirectToAction(nameof(Index));
        }

        AddErrorsToModelState(result);
        return View(request);
    }

    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var companyId = await _currentCompany.GetCompanyIdAsync(cancellationToken);

        if (companyId is null)
        {
            return NotFound();
        }

        var result = await _productService.GetByIdAsync(id, companyId.Value, cancellationToken);

        if (!result.IsSuccess)
        {
            return Failed(result);
        }

        var product = result.Data!;

        return View(new UpdateProductRequest
        {
            Id = product.Id,
            Name = product.Name,
            Sku = product.Sku,
            Barcode = product.Barcode,
            Description = product.Description,
            SellingPrice = product.SellingPrice,
            GstPercent = product.GstPercent,
            CurrentStock = product.CurrentStock,
            ReorderLevel = product.ReorderLevel,
            Status = product.Status,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(UpdateProductRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(request);
        }

        var companyId = await _currentCompany.GetCompanyIdAsync(cancellationToken);

        if (companyId is null)
        {
            return NotFound();
        }

        var result = await _productService.UpdateAsync(companyId.Value, request, cancellationToken);

        if (result.IsSuccess)
        {
            TempData["Success"] = $"Product '{result.Data!.Name}' was updated.";
            return RedirectToAction(nameof(Index));
        }

        if (result.Status == ResultStatus.NotFound)
        {
            return NotFound();
        }

        AddErrorsToModelState(result);
        return View(request);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var companyId = await _currentCompany.GetCompanyIdAsync(cancellationToken);

        if (companyId is null)
        {
            return NotFound();
        }

        var result = await _productService.DeleteAsync(id, companyId.Value, cancellationToken);

        if (!result.IsSuccess)
        {
            return Failed(result);
        }

        TempData["Success"] = "Product was deleted.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Maps a non-success <see cref="ServiceResult"/> onto an HTTP response.</summary>
    private IActionResult Failed(ServiceResult result) => result.Status switch
    {
        ResultStatus.NotFound => NotFound(),
        ResultStatus.Unauthorized => Forbid(),
        _ => Problem(result.Error),
    };

    private void AddErrorsToModelState(ServiceResult result)
    {
        if (result.ValidationErrors.Count > 0)
        {
            foreach (var error in result.ValidationErrors)
            {
                ModelState.AddModelError(string.Empty, error);
            }
        }
        else if (result.Error is not null)
        {
            ModelState.AddModelError(string.Empty, result.Error);
        }
    }
}
