using InventoryErp.Application.Common;
using InventoryErp.Application.DTOs.Quotations;
using InventoryErp.Application.Interfaces;
using InventoryErp.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace InventoryErp.Web.Controllers;

[Authorize]
public class QuotationsController : Controller
{
    private const int DefaultPageSize = 20;

    /// <summary>Dropdowns load every option; the seeded catalogue is small.</summary>
    private const int OptionPageSize = 200;

    private readonly IQuotationService _quotationService;
    private readonly IQuotationPdfService _pdfService;
    private readonly ICustomerService _customerService;
    private readonly IProductService _productService;
    private readonly ICurrentCompanyProvider _currentCompany;

    public QuotationsController(
        IQuotationService quotationService,
        IQuotationPdfService pdfService,
        ICustomerService customerService,
        IProductService productService,
        ICurrentCompanyProvider currentCompany)
    {
        _quotationService = quotationService;
        _pdfService = pdfService;
        _customerService = customerService;
        _productService = productService;
        _currentCompany = currentCompany;
    }

    // -------------------------------------------------------------------- list

    public async Task<IActionResult> Index(
        int page = 1,
        int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        var companyId = await _currentCompany.GetCompanyIdAsync(cancellationToken);

        if (companyId is null)
        {
            return View(new QuotationListViewModel { LoadError = NoCompanyMessage });
        }

        var result = await _quotationService.GetAllAsync(
            companyId.Value, page, pageSize ?? DefaultPageSize, cancellationToken);

        return result.IsSuccess
            ? View(new QuotationListViewModel { Page = result.Data! })
            : View(new QuotationListViewModel { LoadError = Describe(result) });
    }

    // ------------------------------------------------------------------ detail

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var companyId = await _currentCompany.GetCompanyIdAsync(cancellationToken);

        if (companyId is null)
        {
            return NotFound();
        }

        var result = await _quotationService.GetByIdAsync(id, companyId.Value, cancellationToken);

        return result.IsSuccess ? View(result.Data) : Failed(result);
    }

    // --------------------------------------------------------------------- pdf

    /// <summary>Streams the quotation as a PDF. A missing logo does not fail the download.</summary>
    public async Task<IActionResult> Pdf(Guid id, CancellationToken cancellationToken)
    {
        var companyId = await _currentCompany.GetCompanyIdAsync(cancellationToken);

        if (companyId is null)
        {
            return NotFound();
        }

        var result = await _pdfService.GenerateAsync(id, companyId.Value, cancellationToken);

        if (!result.IsSuccess)
        {
            return Failed(result);
        }

        var document = result.Data!;

        // Inline so the browser's PDF viewer opens it; the filename still applies on save.
        Response.Headers.ContentDisposition = $"inline; filename=\"{document.FileName}\"";
        return File(document.Content, document.ContentType);
    }

    // ------------------------------------------------------------------ create

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var model = new QuotationCreateViewModel
        {
            // One blank line so the form is immediately usable without clicking "Add line".
            Lines = [new QuotationLineInputModel()],
        };

        if (await PopulateOptionsAsync(model, cancellationToken) is { } error)
        {
            ModelState.AddModelError(string.Empty, error);
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        QuotationCreateViewModel model,
        CancellationToken cancellationToken)
    {
        var companyId = await _currentCompany.GetCompanyIdAsync(cancellationToken);

        if (companyId is null)
        {
            ModelState.AddModelError(string.Empty, NoCompanyMessage);
            await PopulateOptionsAsync(model, cancellationToken);
            return View(model);
        }

        // Rows the user removed can arrive as empty slots; drop them before validating so an
        // empty row does not masquerade as a real one.
        model.Lines = model.Lines
            .Where(l => l.ProductId is not null || l.Quantity != 0 || l.UnitPrice != 0)
            .ToList();

        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(model, cancellationToken);
            return View(model);
        }

        var result = await _quotationService.CreateQuotationAsync(
            companyId.Value,
            new CreateQuotationRequest
            {
                CustomerId = model.CustomerId ?? Guid.Empty,
                QuotationDate = model.QuotationDate,
                ValidUntil = model.ValidUntil,
                Notes = model.Notes,
                Lines = model.Lines.Select(l => new CreateQuotationLineRequest
                {
                    ProductId = l.ProductId ?? Guid.Empty,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    DiscountPercent = l.DiscountPercent,
                    GstPercent = l.GstPercent,
                }).ToList(),
            },
            cancellationToken);

        if (result.IsSuccess)
        {
            TempData["Success"] = $"Quotation {result.Data!.QuotationNumber} was created.";
            return RedirectToAction(nameof(Details), new { id = result.Data.Id });
        }

        AddServiceErrors(result);
        await PopulateOptionsAsync(model, cancellationToken);
        return View(model);
    }

    // ------------------------------------------------------------------ helpers

    private const string NoCompanyMessage =
        "No company has been configured yet, so quotations cannot be created.";

    /// <summary>
    /// Maps service errors onto <c>ModelState</c>. Errors prefixed "Line N:" are attached to that
    /// row's field so the message appears next to the offending input rather than only in the
    /// summary.
    /// </summary>
    private void AddServiceErrors(ServiceResult result)
    {
        if (result.ValidationErrors.Count == 0)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "The quotation could not be saved.");
            return;
        }

        foreach (var error in result.ValidationErrors)
        {
            if (!TryAttachToLine(error))
            {
                ModelState.AddModelError(string.Empty, error);
            }
        }
    }

    private bool TryAttachToLine(string error)
    {
        const string prefix = "Line ";

        if (!error.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var colon = error.IndexOf(':');

        if (colon < 0 || !int.TryParse(error[prefix.Length..colon], out var lineNumber))
        {
            return false;
        }

        var index = lineNumber - 1;
        var message = error[(colon + 1)..].Trim();

        // Pick the field the message is actually about, so the error lands on the right input.
        var field = message.Contains("quantity", StringComparison.OrdinalIgnoreCase) ? "Quantity"
            : message.Contains("product", StringComparison.OrdinalIgnoreCase) ? "ProductId"
            : message.Contains("unit price", StringComparison.OrdinalIgnoreCase) ? "UnitPrice"
            : message.Contains("discount", StringComparison.OrdinalIgnoreCase) ? "DiscountPercent"
            : message.Contains("GST", StringComparison.OrdinalIgnoreCase) ? "GstPercent"
            : null;

        if (field is null)
        {
            return false;
        }

        ModelState.AddModelError($"Lines[{index}].{field}", char.ToUpperInvariant(message[0]) + message[1..]);
        return true;
    }

    /// <summary>Loads the dropdown options. Returns an error message, or null on success.</summary>
    private async Task<string?> PopulateOptionsAsync(
        QuotationCreateViewModel model,
        CancellationToken cancellationToken)
    {
        var companyId = await _currentCompany.GetCompanyIdAsync(cancellationToken);

        if (companyId is null)
        {
            return NoCompanyMessage;
        }

        var customers = await _customerService.GetAllAsync(
            companyId.Value, 1, OptionPageSize, cancellationToken);

        var products = await _productService.GetAllAsync(
            companyId.Value, 1, OptionPageSize, cancellationToken);

        if (!customers.IsSuccess || !products.IsSuccess)
        {
            return "Could not load customers or products.";
        }

        model.CustomerOptions = customers.Data!.Items
            .Select(c => new SelectListItem(
                c.Code is null ? c.Name : $"{c.Name} ({c.Code})",
                c.Id.ToString()))
            .ToList();

        model.ProductOptions = products.Data!.Items
            .Select(p => new SelectListItem($"{p.Name} — {p.Sku}", p.Id.ToString()))
            .ToList();

        model.ProductDefaults = products.Data.Items
            .ToDictionary(p => p.Id, p => new ProductDefaults(p.SellingPrice, p.GstPercent));

        return null;
    }

    private static string Describe(ServiceResult result)
        => result.ValidationErrors.Count > 0
            ? string.Join(" ", result.ValidationErrors)
            : result.Error ?? "Something went wrong.";

    private IActionResult Failed(ServiceResult result) => result.Status switch
    {
        ResultStatus.NotFound => NotFound(),
        ResultStatus.Unauthorized => Forbid(),
        _ => Problem(result.Error),
    };
}
