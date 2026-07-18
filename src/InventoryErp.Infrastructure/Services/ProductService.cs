using InventoryErp.Application.Common;
using InventoryErp.Application.DTOs.Products;
using InventoryErp.Application.Interfaces;
using InventoryErp.Domain.Entities;
using InventoryErp.Domain.Interfaces;

namespace InventoryErp.Infrastructure.Services;

public sealed class ProductService : IProductService
{
    private readonly IUnitOfWork _unitOfWork;

    public ProductService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    private IRepository<Product> Products => _unitOfWork.Repository<Product>();

    public async Task<ServiceResult<IReadOnlyList<ProductDto>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var products = await Products.ListAsync(cancellationToken: cancellationToken);
        IReadOnlyList<ProductDto> dtos = products
            .OrderBy(p => p.Name)
            .Select(ToDto)
            .ToList();

        return ServiceResult<IReadOnlyList<ProductDto>>.Success(dtos);
    }

    public async Task<ServiceResult<ProductDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await Products.GetByIdAsync(id, cancellationToken);

        return product is null
            ? ServiceResult<ProductDto>.NotFound($"No product with id '{id}'.")
            : ServiceResult<ProductDto>.Success(ToDto(product));
    }

    public async Task<ServiceResult<ProductDto>> CreateAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        var sku = request.Sku.Trim();

        if (string.IsNullOrWhiteSpace(sku))
        {
            return ServiceResult<ProductDto>.Invalid("SKU is required.");
        }

        if (await Products.AnyAsync(p => p.Sku == sku, cancellationToken))
        {
            return ServiceResult<ProductDto>.Conflict($"A product with SKU '{sku}' already exists.");
        }

        var product = new Product
        {
            Sku = sku,
            Name = request.Name.Trim(),
            Description = request.Description,
            UnitPrice = request.UnitPrice,
            QuantityOnHand = request.QuantityOnHand,
            ReorderLevel = request.ReorderLevel,
            Status = request.Status,
        };

        await Products.AddAsync(product, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<ProductDto>.Success(ToDto(product));
    }

    public async Task<ServiceResult<ProductDto>> UpdateAsync(
        UpdateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        var product = await Products.GetByIdAsync(request.Id, cancellationToken);

        if (product is null)
        {
            return ServiceResult<ProductDto>.NotFound($"No product with id '{request.Id}'.");
        }

        var sku = request.Sku.Trim();

        if (await Products.AnyAsync(p => p.Sku == sku && p.Id != request.Id, cancellationToken))
        {
            return ServiceResult<ProductDto>.Conflict($"A product with SKU '{sku}' already exists.");
        }

        product.Sku = sku;
        product.Name = request.Name.Trim();
        product.Description = request.Description;
        product.UnitPrice = request.UnitPrice;
        product.QuantityOnHand = request.QuantityOnHand;
        product.ReorderLevel = request.ReorderLevel;
        product.Status = request.Status;

        Products.Update(product);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<ProductDto>.Success(ToDto(product));
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await Products.GetByIdAsync(id, cancellationToken);

        if (product is null)
        {
            return ServiceResult.NotFound($"No product with id '{id}'.");
        }

        Products.Remove(product);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult.Success();
    }

    private static ProductDto ToDto(Product p) => new()
    {
        Id = p.Id,
        Sku = p.Sku,
        Name = p.Name,
        Description = p.Description,
        UnitPrice = p.UnitPrice,
        QuantityOnHand = p.QuantityOnHand,
        ReorderLevel = p.ReorderLevel,
        Status = p.Status,
        IsBelowReorderLevel = p.IsBelowReorderLevel,
    };
}
