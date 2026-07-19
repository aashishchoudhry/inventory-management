using InventoryErp.Application.Common;
using InventoryErp.Application.DTOs.Dashboard;

namespace InventoryErp.Application.Interfaces;

public interface IDashboardService
{
    /// <summary>
    /// Headline counts for one company.
    /// </summary>
    /// <param name="asOfUtc">
    /// The "today" the date-based figures are measured against. Defaults to the current UTC date;
    /// injectable so the behaviour is testable without freezing the clock.
    /// </param>
    /// <returns>
    /// <c>ValidationFailed</c> for an empty company id, otherwise <c>Success</c>. A company with no
    /// data yields zeros, not an error.
    /// </returns>
    Task<ServiceResult<DashboardStatsDto>> GetStatsAsync(
        Guid companyId,
        DateTime? asOfUtc = null,
        CancellationToken cancellationToken = default);
}
