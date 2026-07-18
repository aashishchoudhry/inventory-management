using InventoryErp.Application.Common;
using InventoryErp.Application.Services;
using InventoryErp.Domain.Entities;
using InventoryErp.Infrastructure.Persistence;
using InventoryErp.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InventoryErp.Application.Tests.Services;

public class CustomerServiceTests : IDisposable
{
    private static readonly Guid CompanyA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CompanyB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly InventoryErpDbContext _context;
    private readonly CustomerService _service;

    public CustomerServiceTests()
    {
        var options = new DbContextOptionsBuilder<InventoryErpDbContext>()
            .UseInMemoryDatabase($"customers-{Guid.NewGuid()}")
            .Options;

        _context = new InventoryErpDbContext(options);
        _service = new CustomerService(new UnitOfWork(_context));
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Seeds directly, since the service is read-only by design.</summary>
    private async Task AddAsync(string name, Guid? companyId = null, string? code = null)
    {
        _context.Customers.Add(new Customer
        {
            CompanyId = companyId ?? CompanyA,
            Name = name,
            Code = code,
            Mobile = "+91 90000 00000",
            City = "Mumbai",
            State = "Maharashtra",
        });

        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetAllAsync_returns_customers_ordered_by_name()
    {
        await AddAsync("Zeta Traders");
        await AddAsync("Alpha Works");
        await AddAsync("Mid Corp");

        var result = await _service.GetAllAsync(CompanyA);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            new[] { "Alpha Works", "Mid Corp", "Zeta Traders" },
            result.Data!.Items.Select(c => c.Name));
    }

    [Fact]
    public async Task GetAllAsync_returns_only_the_requested_companys_customers()
    {
        await AddAsync("Company A Customer", CompanyA);
        await AddAsync("Company B Customer", CompanyB);

        var result = await _service.GetAllAsync(CompanyA);

        Assert.Single(result.Data!.Items);
        Assert.Equal("Company A Customer", result.Data.Items[0].Name);
        Assert.Equal(1, result.Data.TotalCount);
    }

    [Fact]
    public async Task GetAllAsync_maps_all_listed_fields()
    {
        await AddAsync("Patel Engineering", code: "CUST-1001");

        var customer = (await _service.GetAllAsync(CompanyA)).Data!.Items.Single();

        Assert.Equal("Patel Engineering", customer.Name);
        Assert.Equal("CUST-1001", customer.Code);
        Assert.Equal("+91 90000 00000", customer.Mobile);
        Assert.Equal("Mumbai", customer.City);
        Assert.Equal("Maharashtra", customer.State);
    }

    [Fact]
    public async Task GetAllAsync_tolerates_a_null_code()
    {
        // "Walk-in / Counter Sales" in the seed data has no code.
        await AddAsync("Walk-in", code: null);

        var customer = (await _service.GetAllAsync(CompanyA)).Data!.Items.Single();

        Assert.Null(customer.Code);
    }

    [Fact]
    public async Task GetAllAsync_pages_and_reports_the_full_total()
    {
        for (var i = 1; i <= 5; i++)
        {
            await AddAsync($"Customer {i:D2}");
        }

        var result = await _service.GetAllAsync(CompanyA, pageNumber: 2, pageSize: 2);

        Assert.Equal(2, result.Data!.Items.Count);
        Assert.Equal(5, result.Data.TotalCount);   // total is all matches, not the page
        Assert.Equal(3, result.Data.TotalPages);
        Assert.Equal("Customer 03", result.Data.Items[0].Name);
    }

    [Fact]
    public async Task GetAllAsync_returns_an_empty_page_when_there_are_no_customers()
    {
        var result = await _service.GetAllAsync(CompanyA);

        Assert.True(result.IsSuccess);   // empty is success, not NotFound
        Assert.Empty(result.Data!.Items);
        Assert.Equal(0, result.Data.TotalCount);
    }

    [Fact]
    public async Task GetAllAsync_excludes_soft_deleted_customers()
    {
        await AddAsync("Deletable");
        var entity = await _context.Customers.SingleAsync();
        entity.IsDeleted = true;
        await _context.SaveChangesAsync();

        var result = await _service.GetAllAsync(CompanyA);

        Assert.Empty(result.Data!.Items);
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
}
