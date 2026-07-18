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

        // SKU uniqueness is per tenant, matching the composite index on (CompanyId, Sku).
        if (await Products.AnyAsync(p => p.CompanyId == request.CompanyId && p.Sku == sku, cancellationToken))
        {
            return ServiceResult<ProductDto>.Conflict($"A product with SKU '{sku}' already exists.");
        }

        var product = new Product
        {
            CompanyId = request.CompanyId,
            Name = request.Name.Trim(),
            Sku = sku,
            Barcode = request.Barcode,
            Description = request.Description,
            SellingPrice = request.SellingPrice,
            GstPercent = request.GstPercent,
            CurrentStock = request.CurrentStock,
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

        if (await Products.AnyAsync(
                p => p.CompanyId == request.CompanyId && p.Sku == sku && p.Id != request.Id,
                cancellationToken))
        {
            return ServiceResult<ProductDto>.Conflict($"A product with SKU '{sku}' already exists.");
        }

        product.CompanyId = request.CompanyId;
        product.Name = request.Name.Trim();
        product.Sku = sku;
        product.Barcode = request.Barcode;
        product.Description = request.Description;
        product.SellingPrice = request.SellingPrice;
        product.GstPercent = request.GstPercent;
        product.CurrentStock = request.CurrentStock;
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
        CompanyId = p.CompanyId,
        Name = p.Name,
        Sku = p.Sku,
        Barcode = p.Barcode,
        Description = p.Description,
        SellingPrice = p.SellingPrice,
        GstPercent = p.GstPercent,
        CurrentStock = p.CurrentStock,
        ReorderLevel = p.ReorderLevel,
        Status = p.Status,
        IsBelowReorderLevel = p.IsBelowReorderLevel,
    };
}
