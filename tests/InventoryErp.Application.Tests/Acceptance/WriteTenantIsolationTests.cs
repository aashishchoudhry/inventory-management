using InventoryErp.Application.Common;
using InventoryErp.Application.DTOs.Products;
using InventoryErp.Application.DTOs.Quotations;
using InventoryErp.Application.Services;
using InventoryErp.Domain.Entities;
using InventoryErp.Infrastructure.Persistence;
using InventoryErp.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InventoryErp.Application.Tests.Acceptance;

/// <summary>
/// Write-path tenant isolation — the symmetric counterpart to the read-path isolation tests.
/// </summary>
/// <remarks>
/// <para>These exist because of a real defect found in review: <c>CreateProductRequest</c> and
/// <c>UpdateProductRequest</c> carried a bindable <c>CompanyId</c>, and <c>GetByIdAsync</c>,
/// <c>UpdateAsync</c> and <c>DeleteAsync</c> performed no company check at all. A signed-in user
/// could create records under another tenant, and read, edit, move or delete any record by id.</para>
/// <para>The fix removed <c>CompanyId</c> from the request DTOs entirely and made the tenant an
/// explicit service argument. The strongest proof is now structural — <b>there is no longer a
/// property to set</b> — so these tests assert the behaviour that property used to subvert.</para>
/// </remarks>
public class WriteTenantIsolationTests : IDisposable
{
    private static readonly Guid Attacker = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Victim = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly InventoryErpDbContext _context;
    private readonly ProductService _products;
    private readonly QuotationService _quotations;

    private Guid _victimProductId;
    private Guid _victimCustomerId;

    public WriteTenantIsolationTests()
    {
        var options = new DbContextOptionsBuilder<InventoryErpDbContext>()
            .UseInMemoryDatabase($"write-isolation-{Guid.NewGuid()}")
            .Options;

        _context = new InventoryErpDbContext(options);
        var unitOfWork = new UnitOfWork(_context);

        _products = new ProductService(unitOfWork);
        _quotations = new QuotationService(unitOfWork);

        var victimProduct = new Product
        {
            CompanyId = Victim, Name = "Victim Widget", Sku = "VIC-1",
            SellingPrice = 100m, CurrentStock = 50,
        };

        var victimCustomer = new Customer { CompanyId = Victim, Name = "Victim Customer" };

        _context.Products.Add(victimProduct);
        _context.Customers.Add(victimCustomer);
        _context.SaveChanges();

        _victimProductId = victimProduct.Id;
        _victimCustomerId = victimCustomer.Id;
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    private static CreateProductRequest NewProduct(string sku = "ATK-1") => new()
    {
        Name = "Attacker Product",
        Sku = sku,
        SellingPrice = 10m,
        GstPercent = 18m,
        CurrentStock = 1,
    };

    // ------------------------------------------------------------------ create

    [Fact]
    public async Task A_created_product_is_owned_by_the_callers_company()
    {
        var result = await _products.CreateAsync(Attacker, NewProduct());

        Assert.True(result.IsSuccess);
        Assert.Equal(Attacker, result.Data!.CompanyId);

        var stored = await _context.Products.SingleAsync(p => p.Sku == "ATK-1");
        Assert.Equal(Attacker, stored.CompanyId);
    }

    [Fact]
    public async Task Creating_never_writes_a_record_into_another_companys_data()
    {
        // The former attack: post a form with someone else's CompanyId. There is no longer any
        // way to express that — the property does not exist on the request — so the only company
        // that can be written is the one the caller passes.
        await _products.CreateAsync(Attacker, NewProduct());

        var victimProducts = await _context.Products.CountAsync(p => p.CompanyId == Victim);

        Assert.Equal(1, victimProducts);   // unchanged: only the seeded one
    }

    [Fact]
    public async Task The_same_sku_may_exist_in_both_companies_without_collision()
    {
        // Proves the tenant really is part of the uniqueness scope on the write path.
        var result = await _products.CreateAsync(Attacker, NewProduct("VIC-1"));

        Assert.True(result.IsSuccess);
        Assert.Equal(Attacker, result.Data!.CompanyId);
        Assert.Equal(2, await _context.Products.CountAsync(p => p.Sku == "VIC-1"));
    }

    // -------------------------------------------------------------- read by id

    [Fact]
    public async Task Reading_another_companys_product_by_id_returns_NotFound()
    {
        var result = await _products.GetByIdAsync(_victimProductId, Attacker);

        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task The_owning_company_can_still_read_its_own_product()
    {
        var result = await _products.GetByIdAsync(_victimProductId, Victim);

        Assert.True(result.IsSuccess);
        Assert.Equal("Victim Widget", result.Data!.Name);
    }

    // ------------------------------------------------------------------ update

    [Fact]
    public async Task Updating_another_companys_product_returns_NotFound_and_changes_nothing()
    {
        var result = await _products.UpdateAsync(Attacker, new UpdateProductRequest
        {
            Id = _victimProductId,
            Name = "Hijacked",
            Sku = "VIC-1",
            SellingPrice = 1m,
            CurrentStock = 0,
        });

        Assert.Equal(ResultStatus.NotFound, result.Status);

        var stored = await _context.Products.SingleAsync(p => p.Id == _victimProductId);
        Assert.Equal("Victim Widget", stored.Name);
        Assert.Equal(100m, stored.SellingPrice);
        Assert.Equal(Victim, stored.CompanyId);
    }

    [Fact]
    public async Task Updating_cannot_move_a_product_to_another_company()
    {
        var created = await _products.CreateAsync(Attacker, NewProduct());

        await _products.UpdateAsync(Attacker, new UpdateProductRequest
        {
            Id = created.Data!.Id,
            Name = "Renamed",
            Sku = "ATK-1",
            SellingPrice = 20m,
            CurrentStock = 2,
        });

        var stored = await _context.Products.SingleAsync(p => p.Id == created.Data.Id);

        // Ownership is not re-assignable: the update path never writes CompanyId.
        Assert.Equal(Attacker, stored.CompanyId);
        Assert.Equal("Renamed", stored.Name);
    }

    // ------------------------------------------------------------------ delete

    [Fact]
    public async Task Deleting_another_companys_product_returns_NotFound_and_leaves_it_intact()
    {
        var result = await _products.DeleteAsync(_victimProductId, Attacker);

        Assert.Equal(ResultStatus.NotFound, result.Status);

        var stored = await _context.Products
            .IgnoreQueryFilters()
            .SingleAsync(p => p.Id == _victimProductId);

        Assert.False(stored.IsDeleted);
    }

    [Fact]
    public async Task The_owning_company_can_still_delete_its_own_product()
    {
        var result = await _products.DeleteAsync(_victimProductId, Victim);

        Assert.True(result.IsSuccess);
    }

    // --------------------------------------------------------------- quotation

    [Fact]
    public async Task A_created_quotation_is_owned_by_the_callers_company()
    {
        var customer = new Customer { CompanyId = Attacker, Name = "Attacker Customer" };
        var product = new Product { CompanyId = Attacker, Name = "P", Sku = "ATK-P" };
        _context.Customers.Add(customer);
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        var result = await _quotations.CreateQuotationAsync(Attacker, new CreateQuotationRequest
        {
            CustomerId = customer.Id,
            QuotationDate = new DateTime(2026, 7, 18, 0, 0, 0, DateTimeKind.Utc),
            Lines = [new CreateQuotationLineRequest { ProductId = product.Id, Quantity = 1, UnitPrice = 10m }],
        });

        Assert.True(result.IsSuccess);

        var stored = await _context.Quotations.SingleAsync();
        Assert.Equal(Attacker, stored.CompanyId);
    }

    [Fact]
    public async Task A_quotation_cannot_reference_another_companys_customer()
    {
        var result = await _quotations.CreateQuotationAsync(Attacker, new CreateQuotationRequest
        {
            CustomerId = _victimCustomerId,
            QuotationDate = new DateTime(2026, 7, 18, 0, 0, 0, DateTimeKind.Utc),
            Lines = [new CreateQuotationLineRequest { ProductId = _victimProductId, Quantity = 1, UnitPrice = 10m }],
        });

        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Equal(0, await _context.Quotations.CountAsync());
    }

    [Fact]
    public async Task A_quotation_cannot_reference_another_companys_product()
    {
        var customer = new Customer { CompanyId = Attacker, Name = "Attacker Customer" };
        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();

        var result = await _quotations.CreateQuotationAsync(Attacker, new CreateQuotationRequest
        {
            CustomerId = customer.Id,
            QuotationDate = new DateTime(2026, 7, 18, 0, 0, 0, DateTimeKind.Utc),
            Lines = [new CreateQuotationLineRequest { ProductId = _victimProductId, Quantity = 1, UnitPrice = 10m }],
        });

        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Equal(0, await _context.Quotations.CountAsync());
    }
}
