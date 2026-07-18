using InventoryErp.Application.Common;
using InventoryErp.Application.DTOs.Products;
using InventoryErp.Domain.Enums;
using InventoryErp.Infrastructure.Persistence;
using InventoryErp.Infrastructure.Persistence.Repositories;
using InventoryErp.Infrastructure.Services;
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

    private static CreateProductRequest NewRequest(string sku = "SKU-001", Guid? companyId = null) => new()
    {
        CompanyId = companyId ?? CompanyA,
        Name = "Widget",
        Sku = sku,
        Barcode = "8901234567890",
        SellingPrice = 19.99m,
        GstPercent = 18m,
        CurrentStock = 10,
        ReorderLevel = 5,
        Status = ProductStatus.Active,
    };

    [Fact]
    public async Task CreateAsync_persists_the_product_and_returns_it()
    {
        var result = await _service.CreateAsync(NewRequest());

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
        await _service.CreateAsync(NewRequest(companyId: CompanyA));

        var result = await _service.CreateAsync(NewRequest(companyId: CompanyB));

        Assert.True(result.IsSuccess);
        Assert.Equal(CompanyB, result.Data!.CompanyId);
        Assert.Equal(2, await _context.Products.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_returns_Conflict_for_a_duplicate_sku()
    {
        await _service.CreateAsync(NewRequest());

        var result = await _service.CreateAsync(NewRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal(1, await _context.Products.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_returns_Invalid_for_a_blank_sku()
    {
        var result = await _service.CreateAsync(NewRequest("   "));

        Assert.Equal(ResultStatus.ValidationFailed, result.Status);
        Assert.NotEmpty(result.ValidationErrors);
    }

    [Fact]
    public async Task GetByIdAsync_returns_NotFound_for_an_unknown_id()
    {
        var result = await _service.GetByIdAsync(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task UpdateAsync_changes_the_stored_values()
    {
        var created = await _service.CreateAsync(NewRequest());

        var result = await _service.UpdateAsync(new UpdateProductRequest
        {
            Id = created.Data!.Id,
            CompanyId = CompanyA,
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
        var created = await _service.CreateAsync(NewRequest());

        var deleted = await _service.DeleteAsync(created.Data!.Id);
        var all = await _service.GetAllAsync();

        Assert.True(deleted.IsSuccess);
        Assert.Empty(all.Data!);
        Assert.Equal(1, await _context.Products.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task DeleteAsync_returns_NotFound_for_an_unknown_id()
    {
        var result = await _service.DeleteAsync(Guid.NewGuid());

        Assert.Equal(ResultStatus.NotFound, result.Status);
    }
}
