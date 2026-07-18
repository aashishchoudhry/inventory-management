using System.Linq.Expressions;
using InventoryErp.Application.Common;
using InventoryErp.Application.DTOs.Customers;
using InventoryErp.Application.Interfaces;
using InventoryErp.Domain.Common;
using InventoryErp.Domain.Entities;
using InventoryErp.Domain.Interfaces;

namespace InventoryErp.Application.Services;

/// <summary>
/// Application service for customers. Depends only on the <see cref="IUnitOfWork"/> abstraction —
/// no EF Core type reaches this layer.
/// </summary>
public sealed class CustomerService : ICustomerService
{
    /// <summary>Guards against a caller requesting an unbounded page.</summary>
    private const int MaxPageSize = 200;

    private readonly IUnitOfWork _unitOfWork;

    public CustomerService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public Task<ServiceResult<PagedResult<CustomerDto>>> GetAllAsync(
        Guid companyId,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
        => SearchAsync(companyId, keyword: null, pageNumber, pageSize, cancellationToken);

    public async Task<ServiceResult<PagedResult<CustomerDto>>> SearchAsync(
        Guid companyId,
        string? keyword,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var errors = ValidatePaging(companyId, pageNumber, pageSize);

        if (errors.Count > 0)
        {
            return ServiceResult<PagedResult<CustomerDto>>.Invalid(errors);
        }

        // Lower-cased explicitly rather than relying on the database collation — same reasoning
        // as ProductService.SearchAsync.
        var term = keyword?.Trim().ToLowerInvariant();

        // Tenant scoping is part of the predicate: a customer belonging to another company
        // can never be returned.
        Expression<Func<Customer, bool>> predicate = string.IsNullOrWhiteSpace(term)
            ? c => c.CompanyId == companyId
            : c => c.CompanyId == companyId
                   && (c.Name.ToLower().Contains(term)
                       || (c.Code != null && c.Code.ToLower().Contains(term)));

        var page = await _unitOfWork.Repository<Customer>().ListPagedAsync(
            predicate,
            orderBy: c => c.Name,
            pageNumber,
            pageSize,
            cancellationToken: cancellationToken);

        return ServiceResult<PagedResult<CustomerDto>>.Success(page.Map(ToDto));
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

    private static CustomerDto ToDto(Customer c) => new()
    {
        Id = c.Id,
        CompanyId = c.CompanyId,
        Name = c.Name,
        Code = c.Code,
        Mobile = c.Mobile,
        City = c.City,
        State = c.State,
        Address = c.Address,
    };
}
