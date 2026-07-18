using System.Linq.Expressions;
using InventoryErp.Application.Common;
using InventoryErp.Application.DTOs.Products;
using InventoryErp.Application.Interfaces;
using InventoryErp.Domain.Common;
using InventoryErp.Domain.Entities;
using InventoryErp.Domain.Interfaces;

namespace InventoryErp.Application.Services;

/// <summary>
/// Application service for products. Depends only on the <see cref="IUnitOfWork"/> abstraction —
/// no EF Core types reach this layer.
/// </summary>
public sealed class ProductService : IProductService
{
    /// <summary>Guards against a caller requesting an unbounded page.</summary>
    private const int MaxPageSize = 200;

    private readonly IUnitOfWork _unitOfWork;

    public ProductService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    private IRepository<Product> Products => _unitOfWork.Repository<Product>();

    public Task<ServiceResult<PagedResult<ProductDto>>> GetAllAsync(
        Guid companyId,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
        => SearchAsync(companyId, keyword: null, pageNumber, pageSize, cancellationToken);

    public async Task<ServiceResult<PagedResult<ProductDto>>> SearchAsync(
        Guid companyId,
        string? keyword,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var errors = ValidatePaging(companyId, pageNumber, pageSize);

        if (errors.Count > 0)
        {
            return ServiceResult<PagedResult<ProductDto>>.Invalid(errors);
        }

        // Lower-cased explicitly rather than relying on the database collation. SQL Server's
        // default collation is case-insensitive, but that is a database setting, not a guarantee —
        // and the in-memory provider used by tests is case-sensitive. Being explicit makes the
        // behaviour identical everywhere. The cost is that LOWER() prevents an index seek, which
        // is acceptable at this scale but is the first thing to revisit if search gets slow.
        var term = keyword?.Trim().ToLowerInvariant();

        // Tenant scoping is part of the predicate, not an afterthought: every branch below
        // starts from CompanyId, so a product from another company can never be returned.
        Expression<Func<Product, bool>> predicate = string.IsNullOrWhiteSpace(term)
            ? p => p.CompanyId == companyId
            : p => p.CompanyId == companyId
                   && (p.Name.ToLower().Contains(term)
                       || p.Sku.ToLower().Contains(term)
                       || (p.Barcode != null && p.Barcode.ToLower().Contains(term)));

        var page = await Products.ListPagedAsync(
            predicate,
            orderBy: p => p.Name,
            pageNumber,
            pageSize,
            cancellationToken: cancellationToken);

        return ServiceResult<PagedResult<ProductDto>>.Success(page.Map(ToDto));
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

    private static List<string> ValidatePaging(Guid companyId, int pageNumber, int pageSize)
    {
        var errors = new List<string>();

        if (companyId == Guid.Empty)
        {
            errors.Add("A company must be specified.");
        }

        if (pageNumber < 1)
        {
            errors.Add("Page number must be 1 or greater.");
        }

        if (pageSize < 1)
        {
            errors.Add("Page size must be 1 or greater.");
        }
        else if (pageSize > MaxPageSize)
        {
            errors.Add($"Page size must not exceed {MaxPageSize}.");
        }

        return errors;
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
