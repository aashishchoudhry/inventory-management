using InventoryErp.Application.Common;
using InventoryErp.Application.DTOs.Dashboard;
using InventoryErp.Application.Services;
using InventoryErp.Domain.Entities;
using InventoryErp.Infrastructure.Persistence;
using InventoryErp.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InventoryErp.Application.Tests.Services;

public class DashboardServiceTests : IDisposable
{
    /// <summary>Fixed "today" so the date-boundary tests never depend on the wall clock.</summary>
    private static readonly DateTime Today = new(2026, 7, 18, 0, 0, 0, DateTimeKind.Utc);

    private static readonly Guid CompanyB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly InventoryErpDbContext _context;
    private readonly DashboardService _service;
    private Guid _companyId;
    private Guid _customerId;

    public DashboardServiceTests()
    {
        var options = new DbContextOptionsBuilder<InventoryErpDbContext>()
            .UseInMemoryDatabase($"dashboard-{Guid.NewGuid()}")
            .Options;

        _context = new InventoryErpDbContext(options);
        _service = new DashboardService(new UnitOfWork(_context));

        var company = new Company { Name = "Sharma Industrial" };
        var customer = new Customer { CompanyId = company.Id, Name = "Patel Engineering" };

        _context.Companies.Add(company);
        _context.Customers.Add(customer);
        _context.SaveChanges();

        _companyId = company.Id;
        _customerId = customer.Id;
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    private void AddProduct(string sku, Guid? companyId = null) =>
        _context.Products.Add(new Product
        {
            CompanyId = companyId ?? _companyId, Name = "Product " + sku, Sku = sku,
        });

    private void AddCustomer(string name, Guid? companyId = null) =>
        _context.Customers.Add(new Customer
        {
            CompanyId = companyId ?? _companyId, Name = name,
        });

    private void AddQuotation(string number, DateTime date, DateTime? validUntil, Guid? companyId = null) =>
        _context.Quotations.Add(new Quotation
        {
            CompanyId = companyId ?? _companyId,
            QuotationNumber = number,
            CustomerId = _customerId,
            QuotationDate = date,
            ValidUntil = validUntil,
        });

    private Task<DashboardStatsDto> StatsAsync() =>
        _service.GetStatsAsync(_companyId, Today).ContinueWith(t => t.Result.Data!);

    // ----------------------------------------------------------------- counts

    [Fact]
    public async Task Zeroes_for_a_company_with_no_data()
    {
        var stats = await StatsAsync();

        Assert.Equal(0, stats.ProductCount);
        Assert.Equal(0, stats.QuotationsToday);
        Assert.Equal(0, stats.QuotationsExpiringSoon);
        Assert.Equal(1, stats.CustomerCount);   // the fixture's own customer
    }

    [Fact]
    public async Task Counts_products_and_customers()
    {
        AddProduct("A"); AddProduct("B"); AddProduct("C");
        AddCustomer("Second"); AddCustomer("Third");
        await _context.SaveChangesAsync();

        var stats = await StatsAsync();

        Assert.Equal(3, stats.ProductCount);
        Assert.Equal(3, stats.CustomerCount);
    }

    [Fact]
    public async Task Excludes_soft_deleted_records()
    {
        AddProduct("A");
        await _context.SaveChangesAsync();

        var product = await _context.Products.SingleAsync();
        product.IsDeleted = true;
        await _context.SaveChangesAsync();

        Assert.Equal(0, (await StatsAsync()).ProductCount);
    }

    [Fact]
    public async Task Never_counts_another_companys_records()
    {
        AddProduct("MINE");
        AddProduct("THEIRS", CompanyB);
        AddCustomer("Their customer", CompanyB);
        AddQuotation("QT-B", Today, null, CompanyB);
        await _context.SaveChangesAsync();

        var stats = await StatsAsync();

        Assert.Equal(1, stats.ProductCount);
        Assert.Equal(1, stats.CustomerCount);
        Assert.Equal(0, stats.QuotationsToday);
    }

    // -------------------------------------------------------- quotations today

    [Fact]
    public async Task Counts_only_quotations_dated_today()
    {
        AddQuotation("QT-1", Today, null);
        AddQuotation("QT-2", Today.AddHours(23).AddMinutes(59), null);   // still today
        AddQuotation("QT-3", Today.AddDays(-1), null);                   // yesterday
        AddQuotation("QT-4", Today.AddDays(1), null);                    // tomorrow
        await _context.SaveChangesAsync();

        Assert.Equal(2, (await StatsAsync()).QuotationsToday);
    }

    [Fact]
    public async Task A_quotation_saved_with_a_time_component_still_counts_as_today()
    {
        // Guards the half-open range: `QuotationDate == today` would miss this.
        AddQuotation("QT-1", Today.AddHours(14).AddMinutes(37), null);
        await _context.SaveChangesAsync();

        Assert.Equal(1, (await StatsAsync()).QuotationsToday);
    }

    // ------------------------------------------------------- expiring soon

    [Fact]
    public async Task Counts_quotations_expiring_within_the_window()
    {
        AddQuotation("QT-1", Today, Today);                 // expires today
        AddQuotation("QT-2", Today, Today.AddDays(3));      // mid-window
        AddQuotation("QT-3", Today, Today.AddDays(7));      // last day of window
        await _context.SaveChangesAsync();

        Assert.Equal(3, (await StatsAsync()).QuotationsExpiringSoon);
    }

    [Fact]
    public async Task Excludes_quotations_that_already_expired()
    {
        AddQuotation("QT-1", Today.AddDays(-10), Today.AddDays(-1));
        await _context.SaveChangesAsync();

        // Nothing can be done about a lapsed quotation, so it is not a call to action.
        Assert.Equal(0, (await StatsAsync()).QuotationsExpiringSoon);
    }

    [Fact]
    public async Task Excludes_quotations_expiring_beyond_the_window()
    {
        AddQuotation("QT-1", Today, Today.AddDays(8));
        await _context.SaveChangesAsync();

        Assert.Equal(0, (await StatsAsync()).QuotationsExpiringSoon);
    }

    [Fact]
    public async Task Excludes_quotations_that_never_expire()
    {
        AddQuotation("QT-1", Today, validUntil: null);
        await _context.SaveChangesAsync();

        Assert.Equal(0, (await StatsAsync()).QuotationsExpiringSoon);
    }

    [Fact]
    public async Task Counts_the_last_moment_of_the_final_window_day()
    {
        // Boundary: ValidUntil on day 7 with a time component must still be inside.
        AddQuotation("QT-1", Today, Today.AddDays(7).AddHours(23).AddMinutes(59));
        await _context.SaveChangesAsync();

        Assert.Equal(1, (await StatsAsync()).QuotationsExpiringSoon);
    }

    // ------------------------------------------------------------ validation

    [Fact]
    public async Task Rejects_an_empty_company_id()
    {
        var result = await _service.GetStatsAsync(Guid.Empty, Today);

        Assert.Equal(ResultStatus.ValidationFailed, result.Status);
    }

    [Fact]
    public async Task Defaults_to_the_current_date_when_no_as_of_is_given()
    {
        AddQuotation("QT-1", DateTime.UtcNow, null);
        await _context.SaveChangesAsync();

        var result = await _service.GetStatsAsync(_companyId);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Data!.QuotationsToday);
    }
}
