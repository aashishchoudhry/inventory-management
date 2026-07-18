using InventoryErp.Application.Common;
using InventoryErp.Application.DTOs.Customers;
using InventoryErp.Domain.Common;

namespace InventoryErp.Application.Interfaces;

public interface ICustomerService
{
    /// <summary>
    /// One page of customers belonging to <paramref name="companyId"/>, ordered by name.
    /// </summary>
    /// <returns>
    /// <c>Invalid</c> when the company id is empty or the paging arguments are out of range;
    /// otherwise <c>Success</c> with a page, which may contain zero items.
    /// </returns>
    Task<ServiceResult<PagedResult<CustomerDto>>> GetAllAsync(
        Guid companyId,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// One page of customers whose name or code contains <paramref name="keyword"/>. A blank
    /// keyword returns all customers for the company, matching <see cref="GetAllAsync"/>.
    /// </summary>
    Task<ServiceResult<PagedResult<CustomerDto>>> SearchAsync(
        Guid companyId,
        string? keyword,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);
}
