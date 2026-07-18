using InventoryErp.Application.Common;
using InventoryErp.Application.DTOs.Products;
using InventoryErp.Domain.Common;

namespace InventoryErp.Application.Interfaces;

public interface IProductService
{
    /// <summary>
    /// One page of products belonging to <paramref name="companyId"/>, ordered by name.
    /// </summary>
    /// <returns>
    /// <c>Invalid</c> when the company id is empty or the paging arguments are out of range;
    /// otherwise <c>Success</c> with a page (which may contain zero items).
    /// </returns>
    Task<ServiceResult<PagedResult<ProductDto>>> GetAllAsync(
        Guid companyId,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// One page of products belonging to <paramref name="companyId"/> whose name, SKU or barcode
    /// contains <paramref name="keyword"/>. A blank keyword returns all products for the company.
    /// </summary>
    Task<ServiceResult<PagedResult<ProductDto>>> SearchAsync(
        Guid companyId,
        string? keyword,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProductDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ServiceResult<ProductDto>> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default);

    Task<ServiceResult<ProductDto>> UpdateAsync(UpdateProductRequest request, CancellationToken cancellationToken = default);

    Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
