using InventoryErp.Application.Common;
using InventoryErp.Application.DTOs.Dashboard;
using InventoryErp.Application.Interfaces;
using InventoryErp.Domain.Entities;
using InventoryErp.Domain.Interfaces;

namespace InventoryErp.Application.Services;

/// <summary>
/// Headline counts for the dashboard.
/// </summary>
/// <remarks>
/// Each figure is a database-side <c>COUNT</c> rather than a page fetched and measured. The
/// previous ad-hoc dashboard paged products with a 200-row cap and silently understated beyond
/// that; counting in the database has no such ceiling.
/// </remarks>
public sealed class DashboardService : IDashboardService
{
    private readonly IUnitOfWork _unitOfWork;

    public DashboardService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<DashboardStatsDto>> GetStatsAsync(
        Guid companyId,
        DateTime? asOfUtc = null,
        CancellationToken cancellationToken = default)
    {
        if (companyId == Guid.Empty)
        {
            return ServiceResult<DashboardStatsDto>.Invalid("A company must be specified.");
        }

        // Date-only boundaries. QuotationDate carries a time component, so comparing against a
        // half-open [today, tomorrow) range is correct where `== today` would miss anything
        // saved with a non-midnight time.
        var today = (asOfUtc ?? DateTime.UtcNow).Date;
        var tomorrow = today.AddDays(1);
        var windowEnd = today.AddDays(DashboardStatsDto.ExpiringWindowDays + 1);

        // Sequential, not Task.WhenAll: these repositories share one scoped DbContext, which is
        // not thread-safe.
        var products = await _unitOfWork.Repository<Product>()
            .CountAsync(p => p.CompanyId == companyId, cancellationToken);

        var customers = await _unitOfWork.Repository<Customer>()
            .CountAsync(c => c.CompanyId == companyId, cancellationToken);

        var quotations = _unitOfWork.Repository<Quotation>();

        var today_ = await quotations.CountAsync(
            q => q.CompanyId == companyId
                 && q.QuotationDate >= today
                 && q.QuotationDate < tomorrow,
            cancellationToken);

        // Excludes never-expiring quotations (null ValidUntil) and ones that have already
        // lapsed — neither is something anyone can still act on.
        var expiring = await quotations.CountAsync(
            q => q.CompanyId == companyId
                 && q.ValidUntil != null
                 && q.ValidUntil >= today
                 && q.ValidUntil < windowEnd,
            cancellationToken);

        return ServiceResult<DashboardStatsDto>.Success(new DashboardStatsDto
        {
            ProductCount = products,
            CustomerCount = customers,
            QuotationsToday = today_,
            QuotationsExpiringSoon = expiring,
        });
    }
}
