namespace InventoryErp.Application.DTOs.Dashboard;

/// <summary>
/// Headline counts for the dashboard. Every figure is a database-side <c>COUNT</c> scoped to one
/// company, not a length taken from a fetched page.
/// </summary>
public sealed record DashboardStatsDto
{
    /// <summary>Products not soft-deleted, of any status.</summary>
    public int ProductCount { get; init; }

    public int CustomerCount { get; init; }

    /// <summary>Quotations whose <c>QuotationDate</c> falls on today (UTC).</summary>
    public int QuotationsToday { get; init; }

    /// <summary>
    /// Quotations whose <c>ValidUntil</c> falls between today and
    /// <see cref="ExpiringWindowDays"/> days ahead, inclusive. Already-expired and
    /// never-expiring quotations are excluded.
    /// </summary>
    public int QuotationsExpiringSoon { get; init; }

    /// <summary>Look-ahead window for <see cref="QuotationsExpiringSoon"/>.</summary>
    public const int ExpiringWindowDays = 7;
}
