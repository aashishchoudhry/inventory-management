using InventoryErp.Application.Interfaces;
using InventoryErp.Domain.Entities;
using InventoryErp.Domain.Interfaces;

namespace InventoryErp.Application.Services;

/// <inheritdoc cref="ICurrentCompanyProvider"/>
public sealed class CurrentCompanyProvider : ICurrentCompanyProvider
{
    private readonly IUnitOfWork _unitOfWork;
    private Guid? _cached;

    public CurrentCompanyProvider(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid?> GetCompanyIdAsync(CancellationToken cancellationToken = default)
    {
        // Cached per scope (per request), so a page issuing several service calls hits the
        // database once rather than once per call.
        if (_cached is not null)
        {
            return _cached;
        }

        var companies = await _unitOfWork
            .Repository<Company>()
            .ListPagedAsync(predicate: null, orderBy: c => c.CreatedAtUtc, pageNumber: 1, pageSize: 1, cancellationToken);

        _cached = companies.Items.Count > 0 ? companies.Items[0].Id : null;
        return _cached;
    }
}
