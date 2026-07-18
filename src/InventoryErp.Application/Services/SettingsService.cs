using System.Text.RegularExpressions;
using InventoryErp.Application.Common;
using InventoryErp.Application.DTOs.Settings;
using InventoryErp.Application.Interfaces;
using InventoryErp.Domain.Entities;
using InventoryErp.Domain.Interfaces;
using InventoryErp.Shared.Constants;

namespace InventoryErp.Application.Services;

/// <summary>
/// Reads and writes company settings over the <c>CompanySetting</c> key/value table.
/// </summary>
/// <remarks>
/// <para><b>Resolution order</b> for each field: stored setting row → matching <c>Company</c>
/// column → hard-coded default. Several fields (address, GSTIN, PAN) exist both as typed columns
/// and as settings; the setting is authoritative once written, and the column seeds the first
/// read so an unconfigured company shows its real details rather than blanks.</para>
/// <para><b>Writes update both stores.</b> Every field that has a matching <c>Company</c> column
/// is written to the column as well as the setting row, so the two cannot drift apart. Reads still
/// prefer the setting, which keeps the precedence rule simple and unchanged.</para>
/// </remarks>
public sealed partial class SettingsService : ISettingsService
{
    /// <summary>Generous ceiling; a company has a few dozen settings at most.</summary>
    private const int MaxSettings = 500;

    private readonly IUnitOfWork _unitOfWork;

    public SettingsService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    private IRepository<CompanySetting> Settings => _unitOfWork.Repository<CompanySetting>();
    private IRepository<Company> Companies => _unitOfWork.Repository<Company>();

    public async Task<ServiceResult<CompanySettingsDto>> GetSettingsAsync(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        if (companyId == Guid.Empty)
        {
            return ServiceResult<CompanySettingsDto>.Invalid("A company must be specified.");
        }

        var company = await Companies.GetByIdAsync(companyId, cancellationToken);

        if (company is null)
        {
            return ServiceResult<CompanySettingsDto>.NotFound($"No company with id '{companyId}'.");
        }

        var stored = await LoadAsync(companyId, cancellationToken);

        return ServiceResult<CompanySettingsDto>.Success(Resolve(stored, company));
    }

    public async Task<ServiceResult<CompanySettingsDto>> UpdateSettingsAsync(
        Guid companyId,
        CompanySettingsDto settings,
        CancellationToken cancellationToken = default)
    {
        if (companyId == Guid.Empty)
        {
            return ServiceResult<CompanySettingsDto>.Invalid("A company must be specified.");
        }

        var errors = Validate(settings);

        if (errors.Count > 0)
        {
            return ServiceResult<CompanySettingsDto>.Invalid(errors);
        }

        var company = await Companies.GetByIdAsync(companyId, cancellationToken);

        if (company is null)
        {
            return ServiceResult<CompanySettingsDto>.NotFound($"No company with id '{companyId}'.");
        }

        var existing = await LoadEntitiesAsync(companyId, cancellationToken);

        foreach (var (key, value) in ToPairs(settings))
        {
            await UpsertAsync(companyId, existing, key, value, cancellationToken);
        }

        // Mirror onto the typed columns so the two stores cannot diverge. Reads still prefer the
        // setting row; this keeps Company usable by anything that reads it directly.
        ApplyToCompany(company, settings);
        Companies.Update(company);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var stored = await LoadAsync(companyId, cancellationToken);
        return ServiceResult<CompanySettingsDto>.Success(Resolve(stored, company));
    }

    // ------------------------------------------------------------------ upsert

    private async Task UpsertAsync(
        Guid companyId,
        Dictionary<string, CompanySetting> existing,
        string key,
        string value,
        CancellationToken cancellationToken)
    {
        if (existing.TryGetValue(key, out var row))
        {
            if (row.Value == value)
            {
                return; // Unchanged — skip so the audit trail records only real edits.
            }

            row.Value = value;
            Settings.Update(row);
            return;
        }

        await Settings.AddAsync(
            new CompanySetting { CompanyId = companyId, Key = key, Value = value },
            cancellationToken);
    }

    private async Task<Dictionary<string, CompanySetting>> LoadEntitiesAsync(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var page = await Settings.ListPagedAsync(
            predicate: s => s.CompanyId == companyId,
            orderBy: s => s.Key,
            pageNumber: 1,
            pageSize: MaxSettings,
            cancellationToken: cancellationToken);

        // Last write wins if duplicates somehow exist; the filtered unique index should prevent it.
        var map = new Dictionary<string, CompanySetting>(StringComparer.Ordinal);

        foreach (var row in page.Items)
        {
            map[row.Key] = row;
        }

        return map;
    }

    private async Task<Dictionary<string, string>> LoadAsync(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var entities = await LoadEntitiesAsync(companyId, cancellationToken);

        return entities
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Value.Value))
            .ToDictionary(kv => kv.Key, kv => kv.Value.Value!, StringComparer.Ordinal);
    }

    // ---------------------------------------------------------------- mapping

    private static IEnumerable<(string Key, string Value)> ToPairs(CompanySettingsDto s)
    {
        yield return (SettingKeys.Company.Name, s.Name.Trim());
        yield return (SettingKeys.Company.Tagline, s.Tagline.Trim());
        yield return (SettingKeys.Company.Mobile, s.Mobile.Trim());
        yield return (SettingKeys.Company.Email, s.Email.Trim());
        yield return (SettingKeys.Company.Website, s.Website.Trim());
        yield return (SettingKeys.Company.LogoPath, s.LogoPath.Trim());
        yield return (SettingKeys.Company.AddressLine, s.AddressLine.Trim());
        yield return (SettingKeys.Company.City, s.City.Trim());
        yield return (SettingKeys.Company.State, s.State.Trim());
        yield return (SettingKeys.Company.Country, s.Country.Trim());
        yield return (SettingKeys.Company.PinCode, s.PinCode.Trim());
        yield return (SettingKeys.Company.Gstin, s.Gstin.Trim().ToUpperInvariant());
        yield return (SettingKeys.Company.Pan, s.Pan.Trim().ToUpperInvariant());
        yield return (SettingKeys.Documents.InvoiceTerms, s.InvoiceTerms.Trim());
        yield return (SettingKeys.Documents.InvoiceFooter, s.InvoiceFooter.Trim());
        yield return (SettingKeys.Branding.PrimaryAccentColor, s.PrimaryAccentColor.Trim().ToUpperInvariant());
    }

    /// <summary>Setting row → company column → hard default.</summary>
    private static CompanySettingsDto Resolve(Dictionary<string, string> stored, Company company)
    {
        string Get(string key, string? columnFallback, string @default = "")
            => stored.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : !string.IsNullOrWhiteSpace(columnFallback) ? columnFallback! : @default;

        return new CompanySettingsDto
        {
            // Company.Name is required on the entity, so it is always a usable fallback.
            Name = Get(SettingKeys.Company.Name, company.Name),
            Tagline = Get(SettingKeys.Company.Tagline, company.Tagline),
            Mobile = Get(SettingKeys.Company.Mobile, company.Mobile),
            Email = Get(SettingKeys.Company.Email, company.Email),
            Website = Get(SettingKeys.Company.Website, company.Website),
            LogoPath = Get(SettingKeys.Company.LogoPath, company.LogoPath),
            AddressLine = Get(SettingKeys.Company.AddressLine, company.Address),
            City = Get(SettingKeys.Company.City, company.City),
            State = Get(SettingKeys.Company.State, company.State),
            Country = Get(SettingKeys.Company.Country, company.Country, "India"),
            PinCode = Get(SettingKeys.Company.PinCode, company.PinCode),
            Gstin = Get(SettingKeys.Company.Gstin, company.GstNumber),
            Pan = Get(SettingKeys.Company.Pan, company.PanNumber),
            InvoiceTerms = Get(
                SettingKeys.Documents.InvoiceTerms,
                null,
                "Goods once sold will not be taken back. Subject to local jurisdiction."),
            InvoiceFooter = Get(
                SettingKeys.Documents.InvoiceFooter,
                null,
                "This is a computer-generated document."),
            PrimaryAccentColor = Get(
                SettingKeys.Branding.PrimaryAccentColor,
                null,
                CompanySettingsDto.DefaultAccentColor),
        };
    }

    /// <summary>
    /// Mirrors the settings onto the typed <c>Company</c> columns. Blank values are written as
    /// null rather than empty strings, matching how the columns are modelled.
    /// </summary>
    private static void ApplyToCompany(Company company, CompanySettingsDto s)
    {
        static string? OrNull(string value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        // Name is required on the entity; a blank submission must not null it out.
        if (!string.IsNullOrWhiteSpace(s.Name))
        {
            company.Name = s.Name.Trim();
        }

        company.Tagline = OrNull(s.Tagline);
        company.Mobile = OrNull(s.Mobile);
        company.Email = OrNull(s.Email);
        company.Website = OrNull(s.Website);
        company.LogoPath = OrNull(s.LogoPath);
        company.Address = OrNull(s.AddressLine);
        company.City = OrNull(s.City);
        company.State = OrNull(s.State);
        company.Country = OrNull(s.Country);
        company.PinCode = OrNull(s.PinCode);
        company.GstNumber = OrNull(s.Gstin)?.ToUpperInvariant();
        company.PanNumber = OrNull(s.Pan)?.ToUpperInvariant();
    }

    // ------------------------------------------------------------- validation

    private static List<string> Validate(CompanySettingsDto s)
    {
        var errors = new List<string>();

        var gstin = s.Gstin.Trim();
        var pan = s.Pan.Trim();
        var colour = s.PrimaryAccentColor.Trim();

        // Blank is allowed — a company may not be GST-registered — but a supplied value must be
        // the right shape, since it is printed on statutory documents.
        if (gstin.Length > 0 && gstin.Length != 15)
        {
            errors.Add("GSTIN must be exactly 15 characters.");
        }

        if (pan.Length > 0 && pan.Length != 10)
        {
            errors.Add("PAN must be exactly 10 characters.");
        }

        if (colour.Length > 0 && !HexColour().IsMatch(colour))
        {
            errors.Add("Accent colour must be a hex value such as #4F46E5.");
        }

        if (string.IsNullOrWhiteSpace(s.Name))
        {
            errors.Add("Company name is required.");
        }
        else if (s.Name.Length > 200)
        {
            errors.Add("Company name is too long (maximum 200 characters).");
        }

        if (s.Tagline.Length > 300)
        {
            errors.Add("Tagline is too long (maximum 300 characters).");
        }

        if (s.Mobile.Length > 20)
        {
            errors.Add("Mobile is too long (maximum 20 characters).");
        }

        // Deliberately shape-only: a full RFC-compliant check rejects addresses that work.
        if (s.Email.Length > 0 && (!s.Email.Contains('@') || s.Email.Length > 256))
        {
            errors.Add("Email must be a valid address of at most 256 characters.");
        }

        if (s.Website.Length > 256)
        {
            errors.Add("Website is too long (maximum 256 characters).");
        }

        if (s.LogoPath.Length > 500)
        {
            errors.Add("Logo path is too long (maximum 500 characters).");
        }

        if (s.AddressLine.Length > 500)
        {
            errors.Add("Address is too long (maximum 500 characters).");
        }

        if (s.InvoiceTerms.Length > 2000)
        {
            errors.Add("Invoice terms are too long (maximum 2000 characters).");
        }

        if (s.InvoiceFooter.Length > 500)
        {
            errors.Add("Invoice footer is too long (maximum 500 characters).");
        }

        return errors;
    }

    [GeneratedRegex("^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$")]
    private static partial Regex HexColour();
}
