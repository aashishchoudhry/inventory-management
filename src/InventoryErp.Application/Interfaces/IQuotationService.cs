using InventoryErp.Application.Common;
using InventoryErp.Application.DTOs.Quotations;
using InventoryErp.Domain.Common;

namespace InventoryErp.Application.Interfaces;

public interface IQuotationService
{
    /// <summary>
    /// Creates a quotation and its lines, computing every monetary figure server-side.
    /// </summary>
    /// <returns>
    /// <c>ValidationFailed</c> when the request is malformed — no lines, a non-positive quantity,
    /// a negative price, an out-of-range percentage, or a missing company/customer.
    /// <c>NotFound</c> when the customer or a referenced product does not exist within the company.
    /// <c>Conflict</c> when a quotation number could not be allocated uniquely.
    /// Otherwise <c>Success</c> with the created quotation, including its computed totals.
    /// </returns>
    Task<ServiceResult<QuotationDto>> CreateQuotationAsync(
        CreateQuotationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// One page of quotations for a company, newest first. Header fields only — no lines.
    /// </summary>
    Task<ServiceResult<PagedResult<QuotationListItemDto>>> GetAllAsync(
        Guid companyId,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// A single quotation with its lines, or <c>NotFound</c> if it does not exist in the company.
    /// </summary>
    Task<ServiceResult<QuotationDto>> GetByIdAsync(
        Guid id,
        Guid companyId,
        CancellationToken cancellationToken = default);
}
