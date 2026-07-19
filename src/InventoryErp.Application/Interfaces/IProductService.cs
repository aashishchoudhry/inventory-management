using InventoryErp.Application.Common;
using InventoryErp.Application.DTOs.Products;
using InventoryErp.Domain.Common;

namespace InventoryErp.Application.Interfaces;

/// <summary>
/// Product read and write operations.
/// </summary>
/// <remarks>
/// Every method takes <c>companyId</c> explicitly, and no request DTO carries one. The tenant is
/// always supplied by the caller from the signed-in user's company, never accepted from client
/// input — see <c>design-notes.md</c>.
/// </remarks>
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

    /// <summary>
    /// A single product, or <c>NotFound</c> if it does not exist <b>within that company</b>.
    /// Another tenant's product is indistinguishable from a non-existent one.
    /// </summary>
    Task<ServiceResult<ProductDto>> GetByIdAsync(
        Guid id,
        Guid companyId,
        CancellationToken cancellationToken = default);

    /// <summary>Creates a product owned by <paramref name="companyId"/>.</summary>
    Task<ServiceResult<ProductDto>> CreateAsync(
        Guid companyId,
        CreateProductRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a product belonging to <paramref name="companyId"/>. The owning company is never
    /// changed, so a product cannot be moved between tenants.
    /// </summary>
    Task<ServiceResult<ProductDto>> UpdateAsync(
        Guid companyId,
        UpdateProductRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes a product belonging to <paramref name="companyId"/>.</summary>
    Task<ServiceResult> DeleteAsync(
        Guid id,
        Guid companyId,
        CancellationToken cancellationToken = default);
}
