using InventoryErp.Application.Common;
using InventoryErp.Application.DTOs.Products;

namespace InventoryErp.Application.Interfaces;

public interface IProductService
{
    Task<ServiceResult<IReadOnlyList<ProductDto>>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<ServiceResult<ProductDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ServiceResult<ProductDto>> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default);

    Task<ServiceResult<ProductDto>> UpdateAsync(UpdateProductRequest request, CancellationToken cancellationToken = default);

    Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
