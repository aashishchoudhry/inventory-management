using InventoryErp.Application.Common;
using InventoryErp.Application.DTOs.Quotations;
using InventoryErp.Application.Interfaces;
using InventoryErp.Application.Services;
using InventoryErp.Domain.Entities;
using InventoryErp.Infrastructure.Documents;
using InventoryErp.Infrastructure.Persistence;
using InventoryErp.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InventoryErp.Application.Tests.Documents;

public class QuotationPdfServiceTests : IDisposable
{
    private readonly InventoryErpDbContext _context;
    private readonly QuotationPdfService _pdf;
    private readonly StubLogoStorage _logos = new();
    private Guid _companyId;
    private Guid _quotationId;

    public QuotationPdfServiceTests()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        var options = new DbContextOptionsBuilder<InventoryErpDbContext>()
            .UseInMemoryDatabase($"pdf-{Guid.NewGuid()}")
            .Options;

        _context = new InventoryErpDbContext(options);
        var unitOfWork = new UnitOfWork(_context);

        var quotations = new QuotationService(unitOfWork);
        var settings = new SettingsService(unitOfWork);

        _pdf = new QuotationPdfService(
            quotations, settings, _logos, NullLogger<QuotationPdfService>.Instance);

        Seed(quotations).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task Seed(QuotationService quotations)
    {
        var company = new Company
        {
            Name = "Sharma Industrial",
            City = "Mumbai",
            LogoPath = "/uploads/logos/present.png",
        };

        var customer = new Customer { CompanyId = company.Id, Name = "Patel Engineering" };
        var product = new Product { CompanyId = company.Id, Name = "Hex Bolt", Sku = "FST-HB" };

        _context.Companies.Add(company);
        _context.Customers.Add(customer);
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        _companyId = company.Id;

        var created = await quotations.CreateQuotationAsync(company.Id, new CreateQuotationRequest
        {
            CustomerId = customer.Id,
            QuotationDate = new DateTime(2026, 7, 18, 0, 0, 0, DateTimeKind.Utc),
            Lines =
            [
                new CreateQuotationLineRequest
                {
                    ProductId = product.Id, Quantity = 10, UnitPrice = 100m,
                    DiscountPercent = 10m, GstPercent = 18m,
                },
            ],
        });

        _quotationId = created.Data!.Id;
    }

    private static bool IsPdf(byte[] bytes)
        => bytes.Length > 4 && bytes[0] == '%' && bytes[1] == 'P' && bytes[2] == 'D' && bytes[3] == 'F';

    [Fact]
    public async Task Generates_a_pdf_with_the_logo_when_the_file_exists()
    {
        _logos.Bytes = OnePixelPng();

        var result = await _pdf.GenerateAsync(_quotationId, _companyId);

        Assert.True(result.IsSuccess);
        Assert.True(IsPdf(result.Data!.Content));
        Assert.Equal("application/pdf", result.Data.ContentType);
        Assert.Equal("QT-2026-0001.pdf", result.Data.FileName);
    }

    [Fact]
    public async Task Still_generates_when_the_logo_file_is_missing()
    {
        // The exact restored-backup case: LogoPath is set, the file is not there.
        _logos.Bytes = null;

        var result = await _pdf.GenerateAsync(_quotationId, _companyId);

        Assert.True(result.IsSuccess);
        Assert.True(IsPdf(result.Data!.Content));
    }

    [Fact]
    public async Task A_missing_logo_produces_a_smaller_document_than_a_present_one()
    {
        _logos.Bytes = OnePixelPng();
        var withLogo = (await _pdf.GenerateAsync(_quotationId, _companyId)).Data!.Content.Length;

        _logos.Bytes = null;
        var without = (await _pdf.GenerateAsync(_quotationId, _companyId)).Data!.Content.Length;

        // Proves the image was actually omitted rather than silently rendered blank.
        Assert.True(without < withLogo, $"expected {without} < {withLogo}");
    }

    [Fact]
    public async Task An_unreadable_logo_does_not_fail_the_document()
    {
        _logos.Throw = true;

        var result = await _pdf.GenerateAsync(_quotationId, _companyId);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Returns_NotFound_for_an_unknown_quotation()
    {
        var result = await _pdf.GenerateAsync(Guid.NewGuid(), _companyId);

        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task Returns_NotFound_for_another_companys_quotation()
    {
        var result = await _pdf.GenerateAsync(_quotationId, Guid.NewGuid());

        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    /// <summary>Smallest valid PNG, so QuestPDF has real image bytes to embed.</summary>
    private static byte[] OnePixelPng() => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    private sealed class StubLogoStorage : ILogoStorage
    {
        public byte[]? Bytes { get; set; }

        /// <summary>Simulates the storage layer failing rather than simply finding nothing.</summary>
        public bool Throw { get; set; }

        public Task<byte[]?> TryReadAsync(string? webPath, CancellationToken cancellationToken = default)
            => Throw
                ? Task.FromException<byte[]?>(new IOException("disk unavailable"))
                : Task.FromResult(Bytes);

        public Task<ServiceResult<string>> SaveAsync(LogoUpload upload, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteAsync(string webPath, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
