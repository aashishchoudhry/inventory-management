using InventoryErp.Application.Common;
using InventoryErp.Application.DTOs.Quotations;
using InventoryErp.Application.Services;
using InventoryErp.Domain.Entities;
using InventoryErp.Infrastructure.Persistence;
using InventoryErp.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InventoryErp.Application.Tests.Services;

public class QuotationServiceTests : IDisposable
{
    private static readonly Guid CompanyA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CompanyB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly InventoryErpDbContext _context;
    private readonly QuotationService _service;

    private Guid _customerId;
    private Guid _productId;
    private Guid _otherProductId;

    public QuotationServiceTests()
    {
        var options = new DbContextOptionsBuilder<InventoryErpDbContext>()
            .UseInMemoryDatabase($"quotations-{Guid.NewGuid()}")
            .Options;

        _context = new InventoryErpDbContext(options);
        _service = new QuotationService(new UnitOfWork(_context));

        Seed();
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    private void Seed()
    {
        var customer = new Customer { CompanyId = CompanyA, Name = "Patel Engineering" };
        var product = new Product { CompanyId = CompanyA, Name = "Hex Bolt", Sku = "FST-HB" };
        var other = new Product { CompanyId = CompanyA, Name = "Safety Helmet", Sku = "PPE-HLM" };

        _context.Customers.Add(customer);
        _context.Products.AddRange(product, other);
        _context.SaveChanges();

        _customerId = customer.Id;
        _productId = product.Id;
        _otherProductId = other.Id;
    }

    private CreateQuotationRequest NewRequest(params CreateQuotationLineRequest[] lines) => new()
    {
        CustomerId = _customerId,
        QuotationDate = new DateTime(2026, 7, 18, 0, 0, 0, DateTimeKind.Utc),
        ValidUntil = new DateTime(2026, 8, 18, 0, 0, 0, DateTimeKind.Utc),
        Notes = "Sample",
        Lines = lines.Length > 0
            ? lines
            : [new CreateQuotationLineRequest
              {
                  ProductId = _productId,
                  Quantity = 10,
                  UnitPrice = 100m,
                  DiscountPercent = 10m,
                  GstPercent = 18m,
              }],
    };

    // ------------------------------------------------------------- happy path

    [Fact]
    public async Task Creates_the_quotation_and_its_lines()
    {
        var result = await _service.CreateQuotationAsync(CompanyA, NewRequest());

        Assert.True(result.IsSuccess);
        Assert.Equal(1, await _context.Quotations.CountAsync());
        Assert.Equal(1, await _context.QuotationLines.CountAsync());
        Assert.Single(result.Data!.Lines);
    }

    [Fact]
    public async Task Computes_header_totals_from_the_lines()
    {
        // 10 × 100 = 1000 gross, 10% off = 100, taxable 900, GST 18% = 162 → 1062.
        var result = await _service.CreateQuotationAsync(CompanyA, NewRequest());
        var q = result.Data!;

        Assert.Equal(1000m, q.SubTotal);
        Assert.Equal(100m, q.DiscountAmount);
        Assert.Equal(162m, q.TaxAmount);
        Assert.Equal(1062m, q.TotalAmount);
    }

    [Fact]
    public async Task Header_total_equals_the_sum_of_line_totals()
    {
        var result = await _service.CreateQuotationAsync(CompanyA, NewRequest(
            new CreateQuotationLineRequest { ProductId = _productId, Quantity = 7, UnitPrice = 425m, DiscountPercent = 12.5m, GstPercent = 12m },
            new CreateQuotationLineRequest { ProductId = _otherProductId, Quantity = 3, UnitPrice = 1890m, DiscountPercent = 0m, GstPercent = 28m },
            new CreateQuotationLineRequest { ProductId = _productId, Quantity = 1, UnitPrice = 8499m, DiscountPercent = 5m, GstPercent = 18m }));

        var q = result.Data!;

        // The invariant that matters: the stored header reconciles exactly with the stored lines.
        Assert.Equal(q.Lines.Sum(l => l.TotalAmount), q.TotalAmount);
        Assert.Equal(q.Lines.Sum(l => l.GrossAmount), q.SubTotal);
        Assert.Equal(q.Lines.Sum(l => l.DiscountAmount), q.DiscountAmount);
        Assert.Equal(q.Lines.Sum(l => l.TaxAmount), q.TaxAmount);
        Assert.Equal(q.SubTotal - q.DiscountAmount + q.TaxAmount, q.TotalAmount);
    }

    [Fact]
    public async Task Persists_the_computed_amounts_on_each_line()
    {
        await _service.CreateQuotationAsync(CompanyA, NewRequest());

        var line = await _context.QuotationLines.SingleAsync();

        Assert.Equal(162m, line.TaxAmount);
        Assert.Equal(1062m, line.TotalAmount);
        Assert.Equal(10, line.Quantity);
        Assert.Equal(100m, line.UnitPrice);
    }

    [Fact]
    public async Task Links_every_line_to_its_parent_quotation()
    {
        var result = await _service.CreateQuotationAsync(CompanyA, NewRequest(
            new CreateQuotationLineRequest { ProductId = _productId, Quantity = 1, UnitPrice = 10m },
            new CreateQuotationLineRequest { ProductId = _otherProductId, Quantity = 2, UnitPrice = 20m }));

        var lines = await _context.QuotationLines.ToListAsync();

        Assert.Equal(2, lines.Count);
        Assert.All(lines, l => Assert.Equal(result.Data!.Id, l.QuotationId));
    }

    // -------------------------------------------------------- quotation number

    [Fact]
    public async Task Allocates_the_first_number_of_the_year()
    {
        var result = await _service.CreateQuotationAsync(CompanyA, NewRequest());

        Assert.Equal("QT-2026-0001", result.Data!.QuotationNumber);
    }

    [Fact]
    public async Task Increments_the_sequence_for_each_quotation()
    {
        var first = await _service.CreateQuotationAsync(CompanyA, NewRequest());
        var second = await _service.CreateQuotationAsync(CompanyA, NewRequest());
        var third = await _service.CreateQuotationAsync(CompanyA, NewRequest());

        Assert.Equal("QT-2026-0001", first.Data!.QuotationNumber);
        Assert.Equal("QT-2026-0002", second.Data!.QuotationNumber);
        Assert.Equal("QT-2026-0003", third.Data!.QuotationNumber);
    }

    [Fact]
    public async Task Sequence_is_per_year()
    {
        await _service.CreateQuotationAsync(CompanyA, NewRequest());

        var next = NewRequest();
        next.QuotationDate = new DateTime(2027, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        next.ValidUntil = null;

        var result = await _service.CreateQuotationAsync(CompanyA, next);

        Assert.Equal("QT-2027-0001", result.Data!.QuotationNumber);
    }

    [Fact]
    public async Task Sequence_is_per_company()
    {
        await _service.CreateQuotationAsync(CompanyA, NewRequest());

        // A second company starts its own sequence at 0001.
        var customerB = new Customer { CompanyId = CompanyB, Name = "Other Co" };
        var productB = new Product { CompanyId = CompanyB, Name = "Widget", Sku = "W-1" };
        _context.Customers.Add(customerB);
        _context.Products.Add(productB);
        await _context.SaveChangesAsync();

        var result = await _service.CreateQuotationAsync(CompanyB, new CreateQuotationRequest
        {
            CustomerId = customerB.Id,
            QuotationDate = new DateTime(2026, 7, 18, 0, 0, 0, DateTimeKind.Utc),
            Lines = [new CreateQuotationLineRequest { ProductId = productB.Id, Quantity = 1, UnitPrice = 5m }],
        });

        Assert.Equal("QT-2026-0001", result.Data!.QuotationNumber);
    }

    // ------------------------------------------------------------- validation

    [Fact]
    public async Task Rejects_a_quotation_with_no_lines()
    {
        var request = NewRequest();
        request.Lines = [];

        var result = await _service.CreateQuotationAsync(CompanyA, request);

        Assert.Equal(ResultStatus.ValidationFailed, result.Status);
        Assert.Contains(result.ValidationErrors, e => e.Contains("at least one line"));
        Assert.Equal(0, await _context.Quotations.CountAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Rejects_a_non_positive_quantity(int quantity)
    {
        var result = await _service.CreateQuotationAsync(CompanyA, NewRequest(
            new CreateQuotationLineRequest { ProductId = _productId, Quantity = quantity, UnitPrice = 10m }));

        Assert.Equal(ResultStatus.ValidationFailed, result.Status);
        Assert.Contains(result.ValidationErrors, e => e.Contains("quantity must be greater than zero"));
    }

    [Fact]
    public async Task Reports_the_offending_line_number()
    {
        var result = await _service.CreateQuotationAsync(CompanyA, NewRequest(
            new CreateQuotationLineRequest { ProductId = _productId, Quantity = 5, UnitPrice = 10m },
            new CreateQuotationLineRequest { ProductId = _productId, Quantity = 0, UnitPrice = 10m }));

        Assert.Contains(result.ValidationErrors, e => e.StartsWith("Line 2:"));
    }

    [Fact]
    public async Task Rejects_a_negative_unit_price()
    {
        var result = await _service.CreateQuotationAsync(CompanyA, NewRequest(
            new CreateQuotationLineRequest { ProductId = _productId, Quantity = 1, UnitPrice = -1m }));

        Assert.Equal(ResultStatus.ValidationFailed, result.Status);
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(101)]
    public async Task Rejects_an_out_of_range_discount(decimal discountPercent)
    {
        var result = await _service.CreateQuotationAsync(CompanyA, NewRequest(
            new CreateQuotationLineRequest
            {
                ProductId = _productId, Quantity = 1, UnitPrice = 10m, DiscountPercent = discountPercent,
            }));

        Assert.Equal(ResultStatus.ValidationFailed, result.Status);
    }

    [Fact]
    public async Task Rejects_a_valid_until_before_the_quotation_date()
    {
        var request = NewRequest();
        request.ValidUntil = request.QuotationDate.AddDays(-1);

        var result = await _service.CreateQuotationAsync(CompanyA, request);

        Assert.Equal(ResultStatus.ValidationFailed, result.Status);
    }

    // --------------------------------------------------------- missing refs

    [Fact]
    public async Task Returns_NotFound_for_an_unknown_customer()
    {
        var request = NewRequest();
        request.CustomerId = Guid.NewGuid();

        var result = await _service.CreateQuotationAsync(CompanyA, request);

        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Equal(0, await _context.Quotations.CountAsync());
    }

    [Fact]
    public async Task Returns_NotFound_for_an_unknown_product()
    {
        var result = await _service.CreateQuotationAsync(CompanyA, NewRequest(
            new CreateQuotationLineRequest { ProductId = Guid.NewGuid(), Quantity = 1, UnitPrice = 10m }));

        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Equal(0, await _context.Quotations.CountAsync());
    }

    [Fact]
    public async Task Treats_another_companys_customer_as_not_found()
    {
        var foreign = new Customer { CompanyId = CompanyB, Name = "Foreign Co" };
        _context.Customers.Add(foreign);
        await _context.SaveChangesAsync();

        var request = NewRequest();
        request.CustomerId = foreign.Id;

        var result = await _service.CreateQuotationAsync(CompanyA, request);

        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task Treats_another_companys_product_as_not_found()
    {
        var foreign = new Product { CompanyId = CompanyB, Name = "Foreign Widget", Sku = "FW-1" };
        _context.Products.Add(foreign);
        await _context.SaveChangesAsync();

        var result = await _service.CreateQuotationAsync(CompanyA, NewRequest(
            new CreateQuotationLineRequest { ProductId = foreign.Id, Quantity = 1, UnitPrice = 10m }));

        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task Writes_nothing_when_validation_fails()
    {
        var request = NewRequest();
        request.Lines = [];

        await _service.CreateQuotationAsync(CompanyA, request);

        Assert.Equal(0, await _context.Quotations.CountAsync());
        Assert.Equal(0, await _context.QuotationLines.CountAsync());
    }

    // --------------------------------------------------------------- listing

    [Fact]
    public async Task GetAllAsync_returns_an_empty_page_when_there_are_none()
    {
        var result = await _service.GetAllAsync(CompanyA);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!.Items);
        Assert.Equal(0, result.Data.TotalCount);
    }

    [Fact]
    public async Task GetAllAsync_projects_customer_name_and_line_count()
    {
        await _service.CreateQuotationAsync(CompanyA, NewRequest(
            new CreateQuotationLineRequest { ProductId = _productId, Quantity = 1, UnitPrice = 10m },
            new CreateQuotationLineRequest { ProductId = _otherProductId, Quantity = 2, UnitPrice = 20m }));

        var row = (await _service.GetAllAsync(CompanyA)).Data!.Items.Single();

        Assert.Equal("QT-2026-0001", row.QuotationNumber);
        Assert.Equal("Patel Engineering", row.CustomerName);
        Assert.Equal(2, row.LineCount);
        Assert.Equal(50m, row.TotalAmount);
    }

    [Fact]
    public async Task GetAllAsync_returns_newest_first()
    {
        var older = NewRequest();
        older.QuotationDate = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        older.ValidUntil = null;
        await _service.CreateQuotationAsync(CompanyA, older);

        var newer = NewRequest();
        newer.QuotationDate = new DateTime(2026, 9, 9, 0, 0, 0, DateTimeKind.Utc);
        newer.ValidUntil = null;
        await _service.CreateQuotationAsync(CompanyA, newer);

        var items = (await _service.GetAllAsync(CompanyA)).Data!.Items;

        Assert.Equal(new DateTime(2026, 9, 9, 0, 0, 0, DateTimeKind.Utc), items[0].QuotationDate);
    }

    [Fact]
    public async Task GetAllAsync_does_not_leak_across_companies()
    {
        await _service.CreateQuotationAsync(CompanyA, NewRequest());

        var result = await _service.GetAllAsync(CompanyB);

        Assert.Empty(result.Data!.Items);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 500)]
    public async Task GetAllAsync_rejects_out_of_range_paging(int pageNumber, int pageSize)
    {
        var result = await _service.GetAllAsync(CompanyA, pageNumber, pageSize);

        Assert.Equal(ResultStatus.ValidationFailed, result.Status);
    }

    // ---------------------------------------------------------------- details

    [Fact]
    public async Task GetByIdAsync_returns_the_quotation_with_its_lines_and_names()
    {
        var created = await _service.CreateQuotationAsync(CompanyA, NewRequest(
            new CreateQuotationLineRequest
            {
                ProductId = _productId, Quantity = 10, UnitPrice = 100m,
                DiscountPercent = 10m, GstPercent = 18m,
            }));

        var result = await _service.GetByIdAsync(created.Data!.Id, CompanyA);
        var q = result.Data!;

        Assert.True(result.IsSuccess);
        Assert.Equal("Patel Engineering", q.CustomerName);
        Assert.Single(q.Lines);
        Assert.Equal("Hex Bolt", q.Lines[0].ProductName);
        Assert.Equal("FST-HB", q.Lines[0].ProductSku);
        Assert.Equal(1062m, q.TotalAmount);
    }

    [Fact]
    public async Task GetByIdAsync_recomputes_gross_and_discount_for_display()
    {
        // Neither is persisted on the line — they must be derived on read.
        var created = await _service.CreateQuotationAsync(CompanyA, NewRequest());

        var line = (await _service.GetByIdAsync(created.Data!.Id, CompanyA)).Data!.Lines.Single();

        Assert.Equal(1000m, line.GrossAmount);
        Assert.Equal(100m, line.DiscountAmount);
    }

    [Fact]
    public async Task GetByIdAsync_returns_NotFound_for_an_unknown_id()
    {
        var result = await _service.GetByIdAsync(Guid.NewGuid(), CompanyA);

        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task GetByIdAsync_treats_another_companys_quotation_as_not_found()
    {
        var created = await _service.CreateQuotationAsync(CompanyA, NewRequest());

        var result = await _service.GetByIdAsync(created.Data!.Id, CompanyB);

        Assert.Equal(ResultStatus.NotFound, result.Status);
    }
}
