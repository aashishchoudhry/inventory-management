using InventoryErp.Application.Common;
using InventoryErp.Application.DTOs.Settings;

namespace InventoryErp.Application.Interfaces;

public interface ISettingsService
{
    /// <summary>
    /// Reads the company's settings as a strongly-typed object. Any key with no stored row falls
    /// back to the matching <c>Company</c> column, then to a hard-coded default — so this never
    /// returns nulls and never fails because a key is absent.
    /// </summary>
    /// <returns>
    /// <c>ValidationFailed</c> for an empty company id, <c>NotFound</c> if the company does not
    /// exist, otherwise <c>Success</c>.
    /// </returns>
    Task<ServiceResult<CompanySettingsDto>> GetSettingsAsync(
        Guid companyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts one <c>CompanySetting</c> row per field: existing keys are updated, missing keys
    /// inserted. Returns the settings as they now read.
    /// </summary>
    /// <returns>
    /// <c>ValidationFailed</c> if a value is malformed (GSTIN/PAN length, accent colour format),
    /// <c>NotFound</c> if the company does not exist, otherwise <c>Success</c>.
    /// </returns>
    Task<ServiceResult<CompanySettingsDto>> UpdateSettingsAsync(
        Guid companyId,
        CompanySettingsDto settings,
        CancellationToken cancellationToken = default);
}
