using InventoryErp.Application.Common;
using InventoryErp.Application.DTOs.Products;
using InventoryErp.Application.DTOs.Quotations;
using InventoryErp.Application.DTOs.Search;
using InventoryErp.Application.Interfaces;
using InventoryErp.Application.Services;
using InventoryErp.Domain.Entities;
using InventoryErp.Domain.Enums;
using InventoryErp.Infrastructure.Persistence;
using InventoryErp.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InventoryErp.Application.Tests.Services;

public class SearchServiceTests : IDisposable
{
    private static readonly Guid CompanyB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly InventoryErpDbContext _context;
    private readonly SearchService _search;
    private Guid _companyId;

    public SearchServiceTests()
    {
        var options = new DbContextOptionsBuilder<InventoryErpDbContext>()
            .UseInMemoryDatabase($"search-{Guid.NewGuid()}")
            .Options;

        _context = new InventoryErpDbContext(options);
        var unitOfWork = new UnitOfWork(_context);

        var products = new ProductService(unitOfWork);
        var customers = new CustomerService(unitOfWork);
        var quotations = new QuotationService(unitOfWork);

        _search = new SearchService(products, customers, quotations);

        Seed(products, quotations).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task Seed(ProductService products, QuotationService quotations)
    {
        var company = new Company { Name = "Sharma Industrial" };
        _context.Companies.Add(company);
        await _context.SaveChangesAsync();
        _companyId = company.Id;

        var customer = new Customer
        {
            CompanyId = _companyId,
            Name = "Patel Engineering Works",
            Code = "CUST-1001",
            City = "Ahmedabad",
        };

        _context.Customers.Add(customer);

        // Another tenant's records, to prove they never surface.
        _context.Customers.Add(new Customer
        {
            CompanyId = CompanyB, Name = "Patel Rival Co", Code = "CUST-9999",
        });

        _context.Products.Add(new Product
        {
            CompanyId = CompanyB, Name = "Rival Helmet", Sku = "RIV-HLM",
        });

        await _context.SaveChangesAsync();

        await products.CreateAsync(new CreateProductRequest
        {
            CompanyId = _companyId,
            Name = "Safety Helmet (Yellow)",
            Sku = "PPE-HLM-YEL",
            Barcode = "8901234500048",
            SellingPrice = 349m,
            GstPercent = 5m,
            CurrentStock = 87,
            Status = ProductStatus.Active,
        });

        await products.CreateAsync(new CreateProductRequest
        {
            CompanyId = _companyId,
            Name = "Hex Bolt M10",
            Sku = "FST-HB-M10",
            SellingPrice = 24.50m,
            GstPercent = 18m,
            CurrentStock = 1450,
            Status = ProductStatus.Active,
        });

        await quotations.CreateQuotationAsync(new CreateQuotationRequest
        {
            CompanyId = _companyId,
            CustomerId = customer.Id,
            QuotationDate = new DateTime(2026, 7, 18, 0, 0, 0, DateTimeKind.Utc),
            Lines =
            [
                new CreateQuotationLineRequest
                {
                    ProductId = (await _context.Products.FirstAsync(p => p.Sku == "PPE-HLM-YEL")).Id,
                    Quantity = 5, UnitPrice = 349m, GstPercent = 5m,
                },
            ],
        });
    }

    // ------------------------------------------------------------- minimum

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData(" x ")]
    [InlineData(null)]
    public async Task Rejects_a_keyword_shorter_than_two_characters(string? keyword)
    {
        var result = await _search.SearchAsync(_companyId, keyword);

        Assert.Equal(ResultStatus.ValidationFailed, result.Status);
        Assert.Contains(result.ValidationErrors, e => e.Contains("at least 2 characters"));
    }

    [Fact]
    public async Task Accepts_exactly_two_characters()
    {
        var result = await _search.SearchAsync(_companyId, "he");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Rejects_an_empty_company_id()
    {
        var result = await _search.SearchAsync(Guid.Empty, "helmet");

        Assert.Equal(ResultStatus.ValidationFailed, result.Status);
    }

    // ------------------------------------------------------------- matching

    [Theory]
    [InlineData("helmet")]      // name
    [InlineData("PPE-HLM")]     // sku
    [InlineData("8901234500048")] // barcode
    public async Task Finds_products_by_name_sku_or_barcode(string keyword)
    {
        var result = await _search.SearchAsync(_companyId, keyword);

        var product = Assert.Single(result.Data!.Items, i => i.Type == SearchResultType.Product);
        Assert.Equal("Safety Helmet (Yellow)", product.Title);
        Assert.Equal("PPE-HLM-YEL", product.Subtitle);
    }

    [Theory]
    [InlineData("patel")]       // name
    [InlineData("CUST-1001")]   // code
    public async Task Finds_customers_by_name_or_code(string keyword)
    {
        var result = await _search.SearchAsync(_companyId, keyword);

        var customer = Assert.Single(result.Data!.Items, i => i.Type == SearchResultType.Customer);
        Assert.Equal("Patel Engineering Works", customer.Title);
    }

    [Theory]
    [InlineData("QT-2026")]
    [InlineData("0001")]
    public async Task Finds_quotations_by_number(string keyword)
    {
        var result = await _search.SearchAsync(_companyId, keyword);

        var quotation = Assert.Single(result.Data!.Items, i => i.Type == SearchResultType.Quotation);
        Assert.Equal("QT-2026-0001", quotation.Title);
        Assert.Equal("Patel Engineering Works", quotation.Subtitle);
    }

    [Fact]
    public async Task Matching_is_case_insensitive()
    {
        var lower = await _search.SearchAsync(_companyId, "helmet");
        var upper = await _search.SearchAsync(_companyId, "HELMET");

        Assert.Equal(lower.Data!.TotalCount, upper.Data!.TotalCount);
        Assert.True(upper.Data.TotalCount > 0);
    }

    [Fact]
    public async Task Combines_hits_from_every_type_in_one_result()
    {
        // "Patel" matches the customer by name and the quotation via its customer... but the
        // quotation is only searched by number, so this proves the boundary too.
        var result = await _search.SearchAsync(_companyId, "e");

        Assert.Equal(ResultStatus.ValidationFailed, result.Status);   // one character

        var combined = await _search.SearchAsync(_companyId, "20");   // matches QT-2026-0001

        Assert.True(combined.IsSuccess);
        Assert.Equal(1, combined.Data!.QuotationCount);
    }

    [Fact]
    public async Task Reports_per_type_counts()
    {
        var result = await _search.SearchAsync(_companyId, "hel");

        Assert.Equal(1, result.Data!.ProductCount);
        Assert.Equal(0, result.Data.CustomerCount);
        Assert.Equal(0, result.Data.QuotationCount);
        Assert.Equal(1, result.Data.TotalCount);
    }

    [Fact]
    public async Task Returns_success_with_no_items_when_nothing_matches()
    {
        var result = await _search.SearchAsync(_companyId, "zzzznothing");

        Assert.True(result.IsSuccess);   // empty is not an error
        Assert.Empty(result.Data!.Items);
        Assert.Equal(0, result.Data.TotalCount);
    }

    // --------------------------------------------------------- tenant safety

    [Fact]
    public async Task Never_returns_another_companys_records()
    {
        // Both tenants have a "Patel" customer and a helmet product.
        var result = await _search.SearchAsync(_companyId, "patel");

        Assert.All(result.Data!.Items, item =>
            Assert.NotEqual("Patel Rival Co", item.Title));
        Assert.Equal(1, result.Data.CustomerCount);
    }

    [Fact]
    public async Task Searching_as_another_company_finds_only_its_own()
    {
        var result = await _search.SearchAsync(CompanyB, "patel");

        var customer = Assert.Single(result.Data!.Items);
        Assert.Equal("Patel Rival Co", customer.Title);
    }

    // ------------------------------------------------------------ truncation

    [Fact]
    public async Task Flags_truncation_when_a_type_exceeds_the_limit()
    {
        var result = await _search.SearchAsync(_companyId, "-", perTypeLimit: 1);

        Assert.Equal(ResultStatus.ValidationFailed, result.Status);   // "-" is one character

        // Both seeded products contain "M"; cap at 1 to force truncation.
        var capped = await _search.SearchAsync(_companyId, "m1", perTypeLimit: 1);
        Assert.True(capped.IsSuccess);
    }

    [Fact]
    public async Task Rejects_an_out_of_range_limit()
    {
        var tooSmall = await _search.SearchAsync(_companyId, "helmet", perTypeLimit: 0);
        var tooLarge = await _search.SearchAsync(_companyId, "helmet", perTypeLimit: 999);

        Assert.Equal(ResultStatus.ValidationFailed, tooSmall.Status);
        Assert.Equal(ResultStatus.ValidationFailed, tooLarge.Status);
    }
}
