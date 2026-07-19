using InventoryErp.Application.Common;
using InventoryErp.Application.DTOs.Quotations;
using InventoryErp.Application.DTOs.Settings;
using InventoryErp.Application.Interfaces;
using InventoryErp.Application.Services;
using InventoryErp.Domain.Entities;
using InventoryErp.Infrastructure.Documents;
using InventoryErp.Infrastructure.Persistence;
using InventoryErp.Infrastructure.Persistence.Repositories;
using InventoryErp.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InventoryErp.Application.Tests.Acceptance;

/// <summary>
/// Mandatory acceptance test 2 — settings defaults, and the PDF reading whatever is present.
/// </summary>
public class SettingsDefaultsTests : IDisposable
{
    private readonly InventoryErpDbContext _context;
    private readonly SettingsService _settings;
    private readonly QuotationService _quotations;
    private readonly StubLogoStorage _logos = new();

    private Guid _companyId;
    private Guid _quotationId;

    public SettingsDefaultsTests()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        var options = new DbContextOptionsBuilder<InventoryErpDbContext>()
            .UseInMemoryDatabase($"settings-defaults-{Guid.NewGuid()}")
            .Options;

        _context = new InventoryErpDbContext(options);
        var unitOfWork = new UnitOfWork(_context);

        _settings = new SettingsService(unitOfWork);
        _quotations = new QuotationService(unitOfWork);

        Seed().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>A company with nothing but its required name — no settings rows, no optional columns.</summary>
    private async Task Seed()
    {
        var company = new Company { Name = "Bare Minimum Co" };
        var customer = new Customer { CompanyId = company.Id, Name = "A Customer" };
        var product = new Product { CompanyId = company.Id, Name = "A Product", Sku = "SKU-1" };

        _context.Companies.Add(company);
        _context.Customers.Add(customer);
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        _companyId = company.Id;

        var created = await _quotations.CreateQuotationAsync(new CreateQuotationRequest
        {
            CompanyId = company.Id,
            CustomerId = customer.Id,
            QuotationDate = new DateTime(2026, 7, 18, 0, 0, 0, DateTimeKind.Utc),
            Lines =
            [
                new CreateQuotationLineRequest
                {
                    ProductId = product.Id, Quantity = 2, UnitPrice = 100m, GstPercent = 18m,
                },
            ],
        });

        _quotationId = created.Data!.Id;
    }

    private QuotationPdfService Pdf() => new(
        _quotations, _settings, _logos, NullLogger<QuotationPdfService>.Instance);

    private static bool IsPdf(byte[] bytes)
        => bytes.Length > 4 && bytes[0] == '%' && bytes[1] == 'P' && bytes[2] == 'D' && bytes[3] == 'F';

    // ------------------------------------------------------------- defaults

    [Fact]
    public async Task No_setting_rows_exist_for_the_company()
    {
        // Establishes the precondition — otherwise the next test could pass for the wrong reason.
        Assert.Equal(0, await _context.CompanySettings.CountAsync());
    }

    [Fact]
    public async Task GetSettings_returns_the_documented_defaults_when_no_rows_exist()
    {
        var result = await _settings.GetSettingsAsync(_companyId);

        Assert.True(result.IsSuccess);

        var s = result.Data!;

        // Documented in api-contract.md: keys with no row and no Company column fall back to
        // these hard-coded values.
        Assert.Equal("#4F46E5", s.PrimaryAccentColor);
        Assert.Equal(CompanySettingsDto.DefaultAccentColor, s.PrimaryAccentColor);
        Assert.Equal("India", s.Country);
        Assert.Equal(
            "Goods once sold will not be taken back. Subject to local jurisdiction.",
            s.InvoiceTerms);
        Assert.Equal("This is a computer-generated document.", s.InvoiceFooter);

        // The company's required name is the fallback for the name setting.
        Assert.Equal("Bare Minimum Co", s.Name);
    }

    [Fact]
    public async Task Unset_optional_fields_come_back_empty_never_null()
    {
        var s = (await _settings.GetSettingsAsync(_companyId)).Data!;

        // Every property is non-null by contract, so callers never null-check.
        Assert.Equal(string.Empty, s.AddressLine);
        Assert.Equal(string.Empty, s.City);
        Assert.Equal(string.Empty, s.Gstin);
        Assert.Equal(string.Empty, s.Pan);
        Assert.Equal(string.Empty, s.LogoPath);
        Assert.Equal(string.Empty, s.Tagline);
        Assert.Equal(string.Empty, s.Mobile);
        Assert.Equal(string.Empty, s.Email);
    }

    [Fact]
    public async Task A_stored_row_overrides_the_default()
    {
        _context.CompanySettings.Add(new CompanySetting
        {
            CompanyId = _companyId,
            Key = SettingKeys.Branding.PrimaryAccentColor,
            Value = "#0F766E",
        });
        await _context.SaveChangesAsync();

        var s = (await _settings.GetSettingsAsync(_companyId)).Data!;

        Assert.Equal("#0F766E", s.PrimaryAccentColor);
        // Untouched keys still default.
        Assert.Equal("India", s.Country);
    }

    // -------------------------------------------- PDF with partial settings

    [Fact]
    public async Task Pdf_generates_with_no_settings_at_all()
    {
        var result = await Pdf().GenerateAsync(_quotationId, _companyId);

        Assert.True(result.IsSuccess);
        Assert.True(IsPdf(result.Data!.Content));
    }

    [Fact]
    public async Task Pdf_generates_with_only_some_settings_populated()
    {
        // A realistic half-configured company: identity and tax set, address and document text not.
        _context.CompanySettings.AddRange(
            new CompanySetting { CompanyId = _companyId, Key = SettingKeys.Company.Name, Value = "Partly Configured Ltd" },
            new CompanySetting { CompanyId = _companyId, Key = SettingKeys.Company.Gstin, Value = "27AABCU9603R1ZM" });
        await _context.SaveChangesAsync();

        var result = await Pdf().GenerateAsync(_quotationId, _companyId);

        Assert.True(result.IsSuccess);
        Assert.True(IsPdf(result.Data!.Content));
    }

    [Fact]
    public async Task Pdf_generates_when_every_optional_setting_is_blank()
    {
        // Blank strings rather than absent rows — a different path through the fallback logic.
        foreach (var key in new[]
                 {
                     SettingKeys.Company.AddressLine, SettingKeys.Company.City,
                     SettingKeys.Company.State, SettingKeys.Company.Country,
                     SettingKeys.Company.PinCode, SettingKeys.Company.Gstin,
                     SettingKeys.Company.Pan, SettingKeys.Documents.InvoiceTerms,
                     SettingKeys.Documents.InvoiceFooter,
                 })
        {
            _context.CompanySettings.Add(new CompanySetting
            {
                CompanyId = _companyId, Key = key, Value = string.Empty,
            });
        }

        await _context.SaveChangesAsync();

        var result = await Pdf().GenerateAsync(_quotationId, _companyId);

        Assert.True(result.IsSuccess);
        Assert.True(IsPdf(result.Data!.Content));
    }

    [Fact]
    public async Task Pdf_generates_when_the_accent_colour_is_malformed()
    {
        // A bad colour must not reach the renderer and throw mid-document.
        _context.CompanySettings.Add(new CompanySetting
        {
            CompanyId = _companyId,
            Key = SettingKeys.Branding.PrimaryAccentColor,
            Value = "not-a-colour",
        });
        await _context.SaveChangesAsync();

        var result = await Pdf().GenerateAsync(_quotationId, _companyId);

        Assert.True(result.IsSuccess);
        Assert.True(IsPdf(result.Data!.Content));
    }

    [Fact]
    public async Task Pdf_generates_when_the_logo_path_is_set_but_the_file_is_missing()
    {
        _context.CompanySettings.Add(new CompanySetting
        {
            CompanyId = _companyId,
            Key = SettingKeys.Company.LogoPath,
            Value = "/uploads/logos/gone.png",
        });
        await _context.SaveChangesAsync();

        _logos.Bytes = null;   // the restored-backup case

        var result = await Pdf().GenerateAsync(_quotationId, _companyId);

        Assert.True(result.IsSuccess);
        Assert.True(IsPdf(result.Data!.Content));
    }

    private sealed class StubLogoStorage : ILogoStorage
    {
        public byte[]? Bytes { get; set; }

        public Task<byte[]?> TryReadAsync(string? webPath, CancellationToken cancellationToken = default)
            => Task.FromResult(Bytes);

        public Task<ServiceResult<string>> SaveAsync(LogoUpload upload, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteAsync(string webPath, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
