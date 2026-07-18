using InventoryErp.Application.Common;
using InventoryErp.Application.DTOs.Settings;
using InventoryErp.Application.Services;
using InventoryErp.Domain.Entities;
using InventoryErp.Infrastructure.Persistence;
using InventoryErp.Infrastructure.Persistence.Repositories;
using InventoryErp.Shared.Constants;
using Microsoft.EntityFrameworkCore;

namespace InventoryErp.Application.Tests.Services;

public class SettingsServiceTests : IDisposable
{
    private readonly InventoryErpDbContext _context;
    private readonly SettingsService _service;
    private Guid _companyId;

    public SettingsServiceTests()
    {
        var options = new DbContextOptionsBuilder<InventoryErpDbContext>()
            .UseInMemoryDatabase($"settings-{Guid.NewGuid()}")
            .Options;

        _context = new InventoryErpDbContext(options);
        _service = new SettingsService(new UnitOfWork(_context));

        var company = new Company
        {
            Name = "Sharma Industrial",
            Address = "Plot 47, MIDC",
            City = "Mumbai",
            State = "Maharashtra",
            Country = "India",
            PinCode = "400093",
            GstNumber = "27AABCU9603R1ZM",
            PanNumber = "AABCU9603R",
        };

        _context.Companies.Add(company);
        _context.SaveChanges();
        _companyId = company.Id;
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Number of keys written by one save — one row per <c>CompanySettingsDto</c> field.</summary>
    private const int SettingKeyCount = 16;

    private static CompanySettingsDto Sample() => new()
    {
        Name = "Sharma Industrial",
        Tagline = "Fasteners and tools",
        Mobile = "+91 98200 45671",
        Email = "accounts@sharma.in",
        Website = "https://sharma.in",
        AddressLine = "New Address 22",
        City = "Pune",
        State = "Maharashtra",
        Country = "India",
        PinCode = "411001",
        Gstin = "27AAAAA0000A1Z5",
        Pan = "AAAAA0000A",
        InvoiceTerms = "Payment within 30 days.",
        InvoiceFooter = "Thank you for your business.",
        PrimaryAccentColor = "#112233",
    };

    // ------------------------------------------------------------- defaulting

    [Fact]
    public async Task GetSettings_falls_back_to_the_company_columns_when_no_rows_exist()
    {
        var result = await _service.GetSettingsAsync(_companyId);
        var s = result.Data!;

        Assert.True(result.IsSuccess);
        Assert.Equal("Plot 47, MIDC", s.AddressLine);
        Assert.Equal("Mumbai", s.City);
        Assert.Equal("27AABCU9603R1ZM", s.Gstin);
        Assert.Equal("AABCU9603R", s.Pan);
    }

    [Fact]
    public async Task GetSettings_falls_back_to_hard_defaults_for_keys_with_no_column()
    {
        var s = (await _service.GetSettingsAsync(_companyId)).Data!;

        // No Company column backs these, so the hard-coded defaults apply.
        Assert.Equal(CompanySettingsDto.DefaultAccentColor, s.PrimaryAccentColor);
        Assert.NotEmpty(s.InvoiceTerms);
        Assert.NotEmpty(s.InvoiceFooter);
    }

    [Fact]
    public async Task A_stored_setting_overrides_the_company_column()
    {
        _context.CompanySettings.Add(new CompanySetting
        {
            CompanyId = _companyId,
            Key = SettingKeys.Company.City,
            Value = "Nagpur",
        });
        await _context.SaveChangesAsync();

        var s = (await _service.GetSettingsAsync(_companyId)).Data!;

        Assert.Equal("Nagpur", s.City);
    }

    [Fact]
    public async Task A_blank_stored_value_falls_back_rather_than_returning_empty()
    {
        _context.CompanySettings.Add(new CompanySetting
        {
            CompanyId = _companyId,
            Key = SettingKeys.Company.City,
            Value = "   ",
        });
        await _context.SaveChangesAsync();

        var s = (await _service.GetSettingsAsync(_companyId)).Data!;

        Assert.Equal("Mumbai", s.City);
    }

    // ---------------------------------------------------------------- upsert

    [Fact]
    public async Task UpdateSettings_inserts_rows_on_first_save()
    {
        var result = await _service.UpdateSettingsAsync(_companyId, Sample());

        Assert.True(result.IsSuccess);
        Assert.Equal(SettingKeyCount, await _context.CompanySettings.CountAsync());
    }

    [Fact]
    public async Task UpdateSettings_updates_rather_than_duplicating_on_second_save()
    {
        await _service.UpdateSettingsAsync(_companyId, Sample());

        var changed = Sample() with { City = "Nashik" };
        await _service.UpdateSettingsAsync(_companyId, changed);

        // Still one row per key — an upsert, not an append.
        Assert.Equal(SettingKeyCount, await _context.CompanySettings.CountAsync());

        var row = await _context.CompanySettings
            .SingleAsync(s => s.Key == SettingKeys.Company.City);

        Assert.Equal("Nashik", row.Value);
    }

    [Fact]
    public async Task Saved_values_are_read_back_verbatim()
    {
        await _service.UpdateSettingsAsync(_companyId, Sample());

        var s = (await _service.GetSettingsAsync(_companyId)).Data!;

        Assert.Equal("New Address 22", s.AddressLine);
        Assert.Equal("Pune", s.City);
        Assert.Equal("411001", s.PinCode);
        Assert.Equal("Payment within 30 days.", s.InvoiceTerms);
        Assert.Equal("Thank you for your business.", s.InvoiceFooter);
        Assert.Equal("#112233", s.PrimaryAccentColor);
    }

    [Fact]
    public async Task Identifiers_are_upper_cased_and_trimmed()
    {
        await _service.UpdateSettingsAsync(_companyId, Sample() with
        {
            Gstin = "  27aaaaa0000a1z5  ",
            Pan = " aaaaa0000a ",
        });

        var s = (await _service.GetSettingsAsync(_companyId)).Data!;

        Assert.Equal("27AAAAA0000A1Z5", s.Gstin);
        Assert.Equal("AAAAA0000A", s.Pan);
    }

    [Fact]
    public async Task Settings_are_scoped_to_their_company()
    {
        var other = new Company { Name = "Other Co", City = "Delhi" };
        _context.Companies.Add(other);
        await _context.SaveChangesAsync();

        await _service.UpdateSettingsAsync(_companyId, Sample() with { City = "Pune" });

        var otherSettings = (await _service.GetSettingsAsync(other.Id)).Data!;

        // Falls back to its own column, not the first company's setting.
        Assert.Equal("Delhi", otherSettings.City);
    }

    // ------------------------------------------------- company field mirroring

    [Fact]
    public async Task GetSettings_falls_back_to_the_company_identity_columns()
    {
        var s = (await _service.GetSettingsAsync(_companyId)).Data!;

        Assert.Equal("Sharma Industrial", s.Name);
        Assert.Equal(string.Empty, s.LogoPath);   // column is null, no hard default
    }

    [Fact]
    public async Task UpdateSettings_mirrors_onto_the_company_columns()
    {
        // The whole point of writing both stores: they cannot drift apart.
        await _service.UpdateSettingsAsync(_companyId, Sample() with
        {
            Name = "Renamed Industrial",
            Mobile = "+91 90000 11111",
            LogoPath = "/uploads/logos/abc.png",
        });

        var company = await _context.Companies.SingleAsync(c => c.Id == _companyId);

        Assert.Equal("Renamed Industrial", company.Name);
        Assert.Equal("+91 90000 11111", company.Mobile);
        Assert.Equal("/uploads/logos/abc.png", company.LogoPath);
        Assert.Equal("Pune", company.City);
        Assert.Equal("27AAAAA0000A1Z5", company.GstNumber);
    }

    [Fact]
    public async Task Blank_optional_fields_are_stored_as_null_on_the_company()
    {
        await _service.UpdateSettingsAsync(_companyId, Sample() with { Tagline = "", Website = "" });

        var company = await _context.Companies.SingleAsync(c => c.Id == _companyId);

        // Null rather than "", matching how the columns are modelled.
        Assert.Null(company.Tagline);
        Assert.Null(company.Website);
    }

    [Fact]
    public async Task Logo_path_round_trips()
    {
        await _service.UpdateSettingsAsync(
            _companyId, Sample() with { LogoPath = "/uploads/logos/deadbeef.png" });

        var s = (await _service.GetSettingsAsync(_companyId)).Data!;

        Assert.Equal("/uploads/logos/deadbeef.png", s.LogoPath);
    }

    [Fact]
    public async Task Clearing_the_logo_removes_it_from_both_stores()
    {
        await _service.UpdateSettingsAsync(_companyId, Sample() with { LogoPath = "/uploads/logos/x.png" });
        await _service.UpdateSettingsAsync(_companyId, Sample() with { LogoPath = "" });

        var s = (await _service.GetSettingsAsync(_companyId)).Data!;
        var company = await _context.Companies.SingleAsync(c => c.Id == _companyId);

        Assert.Equal(string.Empty, s.LogoPath);
        Assert.Null(company.LogoPath);
    }

    // ------------------------------------------------------------ validation

    [Fact]
    public async Task Rejects_a_blank_company_name()
    {
        var result = await _service.UpdateSettingsAsync(_companyId, Sample() with { Name = "  " });

        Assert.Equal(ResultStatus.ValidationFailed, result.Status);
        Assert.Contains(result.ValidationErrors, e => e.Contains("name is required"));
    }

    [Fact]
    public async Task A_blank_name_never_nulls_the_required_company_column()
    {
        await _service.UpdateSettingsAsync(_companyId, Sample() with { Name = "" });

        var company = await _context.Companies.SingleAsync(c => c.Id == _companyId);

        Assert.Equal("Sharma Industrial", company.Name);
    }

    [Fact]
    public async Task Rejects_an_email_with_no_at_sign()
    {
        var result = await _service.UpdateSettingsAsync(
            _companyId, Sample() with { Email = "not-an-email" });

        Assert.Equal(ResultStatus.ValidationFailed, result.Status);
    }

    [Fact]
    public async Task Allows_a_blank_email()
    {
        var result = await _service.UpdateSettingsAsync(_companyId, Sample() with { Email = "" });

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData("TOOSHORT")]
    [InlineData("WAYTOOLONGGSTIN1234")]
    public async Task Rejects_a_gstin_of_the_wrong_length(string gstin)
    {
        var result = await _service.UpdateSettingsAsync(_companyId, Sample() with { Gstin = gstin });

        Assert.Equal(ResultStatus.ValidationFailed, result.Status);
        Assert.Contains(result.ValidationErrors, e => e.Contains("GSTIN"));
    }

    [Fact]
    public async Task Rejects_a_pan_of_the_wrong_length()
    {
        var result = await _service.UpdateSettingsAsync(_companyId, Sample() with { Pan = "SHORT" });

        Assert.Equal(ResultStatus.ValidationFailed, result.Status);
    }

    [Fact]
    public async Task Allows_blank_tax_identifiers()
    {
        // A company may not be GST-registered.
        var result = await _service.UpdateSettingsAsync(
            _companyId, Sample() with { Gstin = "", Pan = "" });

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData("red")]
    [InlineData("4F46E5")]
    [InlineData("#12345")]
    public async Task Rejects_a_malformed_accent_colour(string colour)
    {
        var result = await _service.UpdateSettingsAsync(
            _companyId, Sample() with { PrimaryAccentColor = colour });

        Assert.Equal(ResultStatus.ValidationFailed, result.Status);
    }

    [Theory]
    [InlineData("#FFF")]
    [InlineData("#4f46e5")]
    public async Task Accepts_three_and_six_digit_hex_colours(string colour)
    {
        var result = await _service.UpdateSettingsAsync(
            _companyId, Sample() with { PrimaryAccentColor = colour });

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Writes_nothing_when_validation_fails()
    {
        await _service.UpdateSettingsAsync(_companyId, Sample() with { Gstin = "BAD" });

        Assert.Equal(0, await _context.CompanySettings.CountAsync());
    }

    [Fact]
    public async Task Returns_NotFound_for_an_unknown_company()
    {
        var result = await _service.GetSettingsAsync(Guid.NewGuid());

        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task Rejects_an_empty_company_id()
    {
        var result = await _service.GetSettingsAsync(Guid.Empty);

        Assert.Equal(ResultStatus.ValidationFailed, result.Status);
    }
}
