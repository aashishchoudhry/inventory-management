using InventoryErp.Application.Common;
using InventoryErp.Application.DTOs.Settings;
using InventoryErp.Application.Interfaces;
using InventoryErp.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryErp.Web.Controllers;

/// <summary>
/// Company settings. One page, one form — settings are per company, and there is currently
/// exactly one company.
/// </summary>
[Authorize]
public class SettingsController : Controller
{
    private readonly ISettingsService _settingsService;
    private readonly ILogoStorage _logoStorage;
    private readonly ICurrentCompanyProvider _currentCompany;

    public SettingsController(
        ISettingsService settingsService,
        ILogoStorage logoStorage,
        ICurrentCompanyProvider currentCompany)
    {
        _settingsService = settingsService;
        _logoStorage = logoStorage;
        _currentCompany = currentCompany;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var companyId = await _currentCompany.GetCompanyIdAsync(cancellationToken);

        if (companyId is null)
        {
            TempData["Error"] = NoCompanyMessage;
            return View(new SettingsViewModel());
        }

        var result = await _settingsService.GetSettingsAsync(companyId.Value, cancellationToken);

        if (!result.IsSuccess)
        {
            TempData["Error"] = Describe(result);
            return View(new SettingsViewModel());
        }

        return View(ToViewModel(result.Data!));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(SettingsViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var companyId = await _currentCompany.GetCompanyIdAsync(cancellationToken);

        if (companyId is null)
        {
            ModelState.AddModelError(string.Empty, NoCompanyMessage);
            return View(model);
        }

        // Resolve the logo before saving settings: if the upload is rejected, nothing is written
        // and the user keeps both their existing logo and their unsaved edits.
        var previousLogo = model.LogoPath;
        var logoPath = model.LogoPath;

        if (model.RemoveLogo)
        {
            logoPath = string.Empty;
        }
        else if (model.LogoFile is { Length: > 0 })
        {
            await using var stream = model.LogoFile.OpenReadStream();

            var upload = new LogoUpload(
                model.LogoFile.FileName,
                model.LogoFile.ContentType,
                model.LogoFile.Length,
                stream);

            var stored = await _logoStorage.SaveAsync(upload, cancellationToken);

            if (!stored.IsSuccess)
            {
                foreach (var error in stored.ValidationErrors.DefaultIfEmpty(stored.Error!))
                {
                    ModelState.AddModelError(nameof(model.LogoFile), error);
                }

                return View(model);
            }

            logoPath = stored.Data!;
        }

        var result = await _settingsService.UpdateSettingsAsync(
            companyId.Value, ToDto(model) with { LogoPath = logoPath }, cancellationToken);

        if (!result.IsSuccess)
        {
            foreach (var error in result.ValidationErrors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            if (result.ValidationErrors.Count == 0)
            {
                ModelState.AddModelError(string.Empty, result.Error ?? "Settings could not be saved.");
            }

            return View(model);
        }

        // Only once the save succeeded: remove the file the company no longer references, so a
        // failed save never leaves it with a missing logo.
        if (!string.IsNullOrWhiteSpace(previousLogo) && previousLogo != logoPath)
        {
            await _logoStorage.DeleteAsync(previousLogo, cancellationToken);
        }

        TempData["Success"] = "Settings saved.";

        // Redirect after POST so a refresh does not resubmit, and so the page re-reads from the
        // database — which is what proves the write actually persisted.
        return RedirectToAction(nameof(Index));
    }

    private const string NoCompanyMessage =
        "No company has been configured yet, so settings cannot be edited.";

    private static SettingsViewModel ToViewModel(CompanySettingsDto d) => new()
    {
        Name = d.Name,
        Tagline = d.Tagline,
        Mobile = d.Mobile,
        Email = d.Email,
        Website = d.Website,
        LogoPath = d.LogoPath,
        AddressLine = d.AddressLine,
        City = d.City,
        State = d.State,
        Country = d.Country,
        PinCode = d.PinCode,
        Gstin = d.Gstin,
        Pan = d.Pan,
        InvoiceTerms = d.InvoiceTerms,
        InvoiceFooter = d.InvoiceFooter,
        PrimaryAccentColor = d.PrimaryAccentColor,
    };

    private static CompanySettingsDto ToDto(SettingsViewModel m) => new()
    {
        Name = m.Name ?? string.Empty,
        Tagline = m.Tagline ?? string.Empty,
        Mobile = m.Mobile ?? string.Empty,
        Email = m.Email ?? string.Empty,
        Website = m.Website ?? string.Empty,
        LogoPath = m.LogoPath ?? string.Empty,
        AddressLine = m.AddressLine ?? string.Empty,
        City = m.City ?? string.Empty,
        State = m.State ?? string.Empty,
        Country = m.Country ?? string.Empty,
        PinCode = m.PinCode ?? string.Empty,
        Gstin = m.Gstin ?? string.Empty,
        Pan = m.Pan ?? string.Empty,
        InvoiceTerms = m.InvoiceTerms ?? string.Empty,
        InvoiceFooter = m.InvoiceFooter ?? string.Empty,
        PrimaryAccentColor = string.IsNullOrWhiteSpace(m.PrimaryAccentColor)
            ? CompanySettingsDto.DefaultAccentColor
            : m.PrimaryAccentColor,
    };

    private static string Describe(ServiceResult result)
        => result.ValidationErrors.Count > 0
            ? string.Join(" ", result.ValidationErrors)
            : result.Error ?? "Something went wrong.";
}
