using InventoryErp.Application.Common;
using InventoryErp.Application.DTOs.Products;
using InventoryErp.Domain.Enums;
using InventoryErp.Application.Services;
using InventoryErp.Infrastructure.Persistence;
using InventoryErp.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InventoryErp.Application.Tests.Services;

public class ProductServiceTests : IDisposable
{
    private readonly InventoryErpDbContext _context;
    private readonly ProductService _service;

    public ProductServiceTests()
    {
        var options = new DbContextOptionsBuilder<InventoryErpDbContext>()
            .UseInMemoryDatabase($"products-{Guid.NewGuid()}")
            .Options;

        _context = new InventoryErpDbContext(options);
        _service = new ProductService(new UnitOfWork(_context));
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    private static readonly Guid CompanyA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CompanyB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static CreateProductRequest NewRequest(
        string sku = "SKU-001",
        Guid? companyId = null,
        string name = "Widget",
        string? barcode = "8901234567890") => new()
    {
        Name = name,
        Sku = sku,
        Barcode = barcode,
        SellingPrice = 19.99m,
        GstPercent = 18m,
        CurrentStock = 10,
        ReorderLevel = 5,
        Status = ProductStatus.Active,
    };

    [Fact]
    public async Task CreateAsync_persists_the_product_and_returns_it()
    {
        var result = await _service.CreateAsync(CompanyA, NewRequest());

        Assert.True(result.IsSuccess);
        Assert.Equal("SKU-001", result.Data!.Sku);
        Assert.Equal(19.99m, result.Data.SellingPrice);
        Assert.Equal(18m, result.Data.GstPercent);
        Assert.Equal(CompanyA, result.Data.CompanyId);
        Assert.Equal(1, await _context.Products.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_allows_the_same_sku_in_a_different_company()
    {
        await _service.CreateAsync(CompanyA, NewRequest());

        var result = await _service.CreateAsync(CompanyB, NewRequest());

        Assert.True(result.IsSuccess);
        Assert.Equal(CompanyB, result.Data!.CompanyId);
        Assert.Equal(2, await _context.Products.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_returns_Conflict_for_a_duplicate_sku()
    {
        await _service.CreateAsync(CompanyA, NewRequest());

        var result = await _service.CreateAsync(CompanyA, NewRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal(1, await _context.Products.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_returns_Invalid_for_a_blank_sku()
    {
        var result = await _service.CreateAsync(CompanyA, NewRequest("   "));

        Assert.Equal(ResultStatus.ValidationFailed, result.Status);
        Assert.NotEmpty(result.ValidationErrors);
    }

    [Fact]
    public async Task GetByIdAsync_returns_NotFound_for_an_unknown_id()
    {
        var result = await _service.GetByIdAsync(Guid.NewGuid(), CompanyA);

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task UpdateAsync_changes_the_stored_values()
    {
        var created = await _service.CreateAsync(CompanyA, NewRequest());

        var result = await _service.UpdateAsync(CompanyA, new UpdateProductRequest
        {
            Id = created.Data!.Id,
            Name = "Renamed widget",
            Sku = "SKU-002",
            SellingPrice = 29.99m,
            GstPercent = 12m,
            CurrentStock = 3,
            ReorderLevel = 5,
            Status = ProductStatus.Discontinued,
        });

        Assert.True(result.IsSuccess);
        Assert.Equal("SKU-002", result.Data!.Sku);
        Assert.Equal(ProductStatus.Discontinued, result.Data.Status);
        Assert.True(result.Data.IsBelowReorderLevel);
    }

    [Fact]
    public async Task DeleteAsync_soft_deletes_and_hides_the_product()
    {
        var created = await _service.CreateAsync(CompanyA, NewRequest());

        var deleted = await _service.DeleteAsync(created.Data!.Id, CompanyA);
        var all = await _service.GetAllAsync(CompanyA);

        Assert.True(deleted.IsSuccess);
        Assert.Empty(all.Data!.Items);
        Assert.Equal(1, await _context.Products.IgnoreQueryFilters().CountAsync());
    }

    // ---------------------------------------------------------------- listing

    [Fact]
    public async Task GetAllAsync_returns_only_the_requested_companys_products()
    {
        await _service.CreateAsync(CompanyA, NewRequest("SKU-A"));
        await _service.CreateAsync(CompanyB, NewRequest("SKU-B"));

        var result = await _service.GetAllAsync(CompanyA);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!.Items);
        Assert.Equal(CompanyA, result.Data.Items[0].CompanyId);
        Assert.Equal(1, result.Data.TotalCount);
    }

    [Fact]
    public async Task GetAllAsync_pages_and_reports_the_full_total()
    {
        for (var i = 1; i <= 5; i++)
        {
            await _service.CreateAsync(CompanyA, NewRequest($"SKU-{i:D3}"));
        }

        var result = await _service.GetAllAsync(CompanyA, pageNumber: 2, pageSize: 2);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.Items.Count);
        Assert.Equal(5, result.Data.TotalCount);   // total is all matches, not the page
        Assert.Equal(3, result.Data.TotalPages);
        Assert.True(result.Data.HasPreviousPage);
        Assert.True(result.Data.HasNextPage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task SearchAsync_with_a_blank_keyword_returns_everything(string? keyword)
    {
        await _service.CreateAsync(CompanyA, NewRequest("SKU-A"));
        await _service.CreateAsync(CompanyA, NewRequest("SKU-B"));

        var result = await _service.SearchAsync(CompanyA, keyword);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.TotalCount);
    }

    [Fact]
    public async Task GetAllAsync_rejects_an_empty_company_id()
    {
        var result = await _service.GetAllAsync(Guid.Empty);

        Assert.Equal(ResultStatus.ValidationFailed, result.Status);
        Assert.NotEmpty(result.ValidationErrors);
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

    // ----------------------------------------------------------------- search

    [Fact]
    public async Task SearchAsync_matches_on_name()
    {
        await _service.CreateAsync(CompanyA, NewRequest("SKU-A", name: "Hex Bolt M10"));
        await _service.CreateAsync(CompanyA, NewRequest("SKU-B", name: "Safety Helmet"));

        var result = await _service.SearchAsync(CompanyA, "helmet");

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!.Items);
        Assert.Equal("Safety Helmet", result.Data.Items[0].Name);
    }

    [Fact]
    public async Task SearchAsync_matches_on_sku()
    {
        await _service.CreateAsync(CompanyA, NewRequest("FST-HB-M10"));
        await _service.CreateAsync(CompanyA, NewRequest("PPE-HLM-YEL"));

        var result = await _service.SearchAsync(CompanyA, "PPE");

        Assert.Single(result.Data!.Items);
        Assert.Equal("PPE-HLM-YEL", result.Data.Items[0].Sku);
    }

    [Fact]
    public async Task SearchAsync_matches_on_barcode()
    {
        await _service.CreateAsync(CompanyA, NewRequest("SKU-A", barcode: "8901234500017"));
        await _service.CreateAsync(CompanyA, NewRequest("SKU-B", barcode: "8901234500024"));

        var result = await _service.SearchAsync(CompanyA, "500024");

        Assert.Single(result.Data!.Items);
        Assert.Equal("SKU-B", result.Data.Items[0].Sku);
    }

    [Fact]
    public async Task SearchAsync_tolerates_products_with_no_barcode()
    {
        // Guards the null-check in the predicate: without it this throws rather than returning.
        await _service.CreateAsync(CompanyA, NewRequest("SKU-A", barcode: null));

        var result = await _service.SearchAsync(CompanyA, "890");

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!.Items);
    }

    [Fact]
    public async Task SearchAsync_does_not_leak_across_companies()
    {
        await _service.CreateAsync(CompanyA, NewRequest("SKU-A", name: "Shared Widget"));
        await _service.CreateAsync(CompanyB, NewRequest("SKU-B", name: "Shared Widget"));

        var result = await _service.SearchAsync(CompanyA, "Shared");

        Assert.Single(result.Data!.Items);
        Assert.Equal(CompanyA, result.Data.Items[0].CompanyId);
    }

    [Fact]
    public async Task SearchAsync_returns_an_empty_page_when_nothing_matches()
    {
        await _service.CreateAsync(CompanyA, NewRequest());

        var result = await _service.SearchAsync(CompanyA, "no-such-product");

        Assert.True(result.IsSuccess);   // an empty result is success, not NotFound
        Assert.Empty(result.Data!.Items);
        Assert.Equal(0, result.Data.TotalCount);
    }

    [Fact]
    public async Task SearchAsync_excludes_soft_deleted_products()
    {
        var created = await _service.CreateAsync(CompanyA, NewRequest("SKU-A", name: "Deletable"));
        await _service.DeleteAsync(created.Data!.Id, CompanyA);

        var result = await _service.SearchAsync(CompanyA, "Deletable");

        Assert.Empty(result.Data!.Items);
    }

    [Fact]
    public async Task DeleteAsync_returns_NotFound_for_an_unknown_id()
    {
        var result = await _service.DeleteAsync(Guid.NewGuid(), CompanyA);

        Assert.Equal(ResultStatus.NotFound, result.Status);
    }
}
