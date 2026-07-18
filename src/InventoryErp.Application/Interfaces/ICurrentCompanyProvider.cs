namespace InventoryErp.Application.Interfaces;

/// <summary>
/// Supplies the company (tenant) that the current request operates within.
/// </summary>
/// <remarks>
/// <para>
/// <b>Interim implementation.</b> There is no tenant context yet — no claim on the signed-in user
/// links them to a company — so the current implementation resolves the single seeded company.
/// That is correct while exactly one company exists and wrong the moment a second is added.
/// </para>
/// <para>
/// The interface exists so callers depend on the concept rather than the shortcut: when a company
/// claim is added to <c>ApplicationUser</c>, only the implementation changes.
/// </para>
/// </remarks>
public interface ICurrentCompanyProvider
{
    /// <summary>Returns the current company id, or null when none can be resolved.</summary>
    Task<Guid?> GetCompanyIdAsync(CancellationToken cancellationToken = default);
}
