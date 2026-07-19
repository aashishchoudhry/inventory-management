using InventoryErp.Application.Common;
using InventoryErp.Application.DTOs.Quotations;
using InventoryErp.Application.Services;
using InventoryErp.Domain.Entities;
using InventoryErp.Infrastructure.Persistence;
using InventoryErp.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InventoryErp.Application.Tests.Acceptance;

/// <summary>
/// Mandatory acceptance test 1 — quotation calculation.
/// </summary>
/// <remarks>
/// Expected values are <b>hand-calculated</b> and written out in the comments below, not derived
/// from the implementation. If the calculation changes, these fail — which is the point.
/// </remarks>
public class QuotationCalculationTests : IDisposable
{
    private readonly InventoryErpDbContext _context;
    private readonly QuotationService _service;

    private Guid _companyId;
    private Guid _customerId;
    private Guid _hexBoltId;
    private Guid _helmetId;

    public QuotationCalculationTests()
    {
        var options = new DbContextOptionsBuilder<InventoryErpDbContext>()
            .UseInMemoryDatabase($"quote-calc-{Guid.NewGuid()}")
            .Options;

        _context = new InventoryErpDbContext(options);
        _service = new QuotationService(new UnitOfWork(_context));

        var company = new Company { Name = "Sharma Industrial" };
        var customer = new Customer { CompanyId = company.Id, Name = "Patel Engineering" };
        var hexBolt = new Product { CompanyId = company.Id, Name = "Hex Bolt M10", Sku = "FST-HB-M10" };
        var helmet = new Product { CompanyId = company.Id, Name = "Safety Helmet", Sku = "PPE-HLM-YEL" };

        _context.Companies.Add(company);
        _context.Customers.Add(customer);
        _context.Products.AddRange(hexBolt, helmet);
        _context.SaveChanges();

        _companyId = company.Id;
        _customerId = customer.Id;
        _hexBoltId = hexBolt.Id;
        _helmetId = helmet.Id;
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    private CreateQuotationRequest Request(params CreateQuotationLineRequest[] lines) => new()
    {
        CustomerId = _customerId,
        QuotationDate = new DateTime(2026, 7, 18, 0, 0, 0, DateTimeKind.Utc),
        Lines = lines,
    };

    // =====================================================================
    //  Two products, known inputs, hand-calculated expected totals.
    //
    //  Line 1 — Hex Bolt: qty 10 @ 24.50, 10% discount, 18% GST
    //      gross    = 10 × 24.50            = 245.00
    //      discount = 245.00 × 0.10         =  24.50
    //      taxable  = 245.00 − 24.50        = 220.50
    //      tax      = 220.50 × 0.18         =  39.69
    //      total    = 220.50 + 39.69        = 260.19
    //
    //  Line 2 — Safety Helmet: qty 5 @ 349.00, 0% discount, 5% GST
    //      gross    = 5 × 349.00            = 1745.00
    //      discount = 0                     =    0.00
    //      taxable  = 1745.00               = 1745.00
    //      tax      = 1745.00 × 0.05        =   87.25
    //      total    = 1745.00 + 87.25       = 1832.25
    //
    //  Header
    //      subTotal       = 245.00 + 1745.00            = 1990.00
    //      discountAmount = 24.50 + 0.00                =   24.50
    //      taxAmount      = 39.69 + 87.25               =  126.94
    //      totalAmount    = 1990.00 − 24.50 + 126.94    = 2092.44
    // =====================================================================

    [Fact]
    public async Task Quotation_with_two_lines_produces_the_hand_calculated_totals()
    {
        var result = await _service.CreateQuotationAsync(_companyId, Request(
            new CreateQuotationLineRequest
            {
                ProductId = _hexBoltId, Quantity = 10, UnitPrice = 24.50m,
                DiscountPercent = 10m, GstPercent = 18m,
            },
            new CreateQuotationLineRequest
            {
                ProductId = _helmetId, Quantity = 5, UnitPrice = 349.00m,
                DiscountPercent = 0m, GstPercent = 5m,
            }));

        Assert.True(result.IsSuccess);

        var quotation = result.Data!;

        Assert.Equal(1990.00m, quotation.SubTotal);
        Assert.Equal(24.50m, quotation.DiscountAmount);
        Assert.Equal(126.94m, quotation.TaxAmount);
        Assert.Equal(2092.44m, quotation.TotalAmount);
    }

    [Fact]
    public async Task Each_line_produces_its_own_hand_calculated_amounts()
    {
        var result = await _service.CreateQuotationAsync(_companyId, Request(
            new CreateQuotationLineRequest
            {
                ProductId = _hexBoltId, Quantity = 10, UnitPrice = 24.50m,
                DiscountPercent = 10m, GstPercent = 18m,
            },
            new CreateQuotationLineRequest
            {
                ProductId = _helmetId, Quantity = 5, UnitPrice = 349.00m,
                DiscountPercent = 0m, GstPercent = 5m,
            }));

        var bolt = result.Data!.Lines.Single(l => l.ProductId == _hexBoltId);
        var helmet = result.Data.Lines.Single(l => l.ProductId == _helmetId);

        Assert.Equal(245.00m, bolt.GrossAmount);
        Assert.Equal(24.50m, bolt.DiscountAmount);
        Assert.Equal(39.69m, bolt.TaxAmount);
        Assert.Equal(260.19m, bolt.TotalAmount);

        Assert.Equal(1745.00m, helmet.GrossAmount);
        Assert.Equal(0.00m, helmet.DiscountAmount);
        Assert.Equal(87.25m, helmet.TaxAmount);
        Assert.Equal(1832.25m, helmet.TotalAmount);
    }

    [Fact]
    public async Task Totals_are_persisted_not_only_returned()
    {
        await _service.CreateQuotationAsync(_companyId, Request(
            new CreateQuotationLineRequest
            {
                ProductId = _hexBoltId, Quantity = 10, UnitPrice = 24.50m,
                DiscountPercent = 10m, GstPercent = 18m,
            },
            new CreateQuotationLineRequest
            {
                ProductId = _helmetId, Quantity = 5, UnitPrice = 349.00m,
                DiscountPercent = 0m, GstPercent = 5m,
            }));

        // Read back from the store, so a correct DTO built over a wrong entity cannot pass.
        var stored = await _context.Quotations.SingleAsync();

        Assert.Equal(1990.00m, stored.SubTotal);
        Assert.Equal(24.50m, stored.DiscountAmount);
        Assert.Equal(126.94m, stored.TaxAmount);
        Assert.Equal(2092.44m, stored.TotalAmount);
    }

    [Fact]
    public async Task Gst_is_charged_on_the_discounted_amount_not_the_gross()
    {
        // Pins the order of operations. Taxing the gross would give 44.10 rather than 39.69 —
        // a 4.41 overstatement on this line alone, and wrong on every discounted invoice.
        var result = await _service.CreateQuotationAsync(_companyId, Request(
            new CreateQuotationLineRequest
            {
                ProductId = _hexBoltId, Quantity = 10, UnitPrice = 24.50m,
                DiscountPercent = 10m, GstPercent = 18m,
            }));

        Assert.Equal(39.69m, result.Data!.TaxAmount);
        Assert.NotEqual(44.10m, result.Data.TaxAmount);
    }

    // ------------------------------------------------- zero-quantity rejection

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task A_line_with_a_non_positive_quantity_is_rejected(int quantity)
    {
        var result = await _service.CreateQuotationAsync(_companyId, Request(
            new CreateQuotationLineRequest
            {
                ProductId = _hexBoltId, Quantity = quantity, UnitPrice = 24.50m,
            }));

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.ValidationFailed, result.Status);
        Assert.Contains(result.ValidationErrors, e => e.Contains("quantity must be greater than zero"));
    }

    [Fact]
    public async Task A_rejected_quotation_writes_nothing_to_the_database()
    {
        await _service.CreateQuotationAsync(_companyId, Request(
            new CreateQuotationLineRequest { ProductId = _hexBoltId, Quantity = 5, UnitPrice = 10m },
            new CreateQuotationLineRequest { ProductId = _helmetId, Quantity = 0, UnitPrice = 10m }));

        // One bad line must reject the whole quotation, not save the good line.
        Assert.Equal(0, await _context.Quotations.CountAsync());
        Assert.Equal(0, await _context.QuotationLines.CountAsync());
    }

    [Fact]
    public async Task The_rejection_message_identifies_which_line_is_wrong()
    {
        var result = await _service.CreateQuotationAsync(_companyId, Request(
            new CreateQuotationLineRequest { ProductId = _hexBoltId, Quantity = 5, UnitPrice = 10m },
            new CreateQuotationLineRequest { ProductId = _helmetId, Quantity = 0, UnitPrice = 10m }));

        Assert.Contains(result.ValidationErrors, e => e.StartsWith("Line 2:"));
    }
}
