using InventoryErp.Application.Common;
using InventoryErp.Application.DTOs.Products;
using InventoryErp.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryErp.Web.Controllers;

[Authorize]
public class ProductsController : Controller
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var result = await _productService.GetAllAsync(cancellationToken);

        return result.IsSuccess
            ? View(result.Data)
            : Problem(result.Error);
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var result = await _productService.GetByIdAsync(id, cancellationToken);

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

        var result = await _productService.CreateAsync(request, cancellationToken);

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
        var result = await _productService.GetByIdAsync(id, cancellationToken);

        if (!result.IsSuccess)
        {
            return Failed(result);
        }

        var product = result.Data!;

        return View(new UpdateProductRequest
        {
            Id = product.Id,
            CompanyId = product.CompanyId,
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

        var result = await _productService.UpdateAsync(request, cancellationToken);

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
        var result = await _productService.DeleteAsync(id, cancellationToken);

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
