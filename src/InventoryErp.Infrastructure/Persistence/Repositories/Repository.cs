using System.Linq.Expressions;
using InventoryErp.Domain.Common;
using InventoryErp.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InventoryErp.Infrastructure.Persistence.Repositories;

public class Repository<T> : IRepository<T> where T : BaseEntity
{
    private readonly DbSet<T> _set;

    public Repository(InventoryErpDbContext context)
    {
        _set = context.Set<T>();
    }

    public Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _set.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<IReadOnlyList<T>> ListAsync(
        Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        var query = _set.AsNoTracking();

        if (predicate is not null)
        {
            query = query.Where(predicate);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<T>> ListPagedAsync<TKey>(
        Expression<Func<T, bool>>? predicate,
        Expression<Func<T, TKey>> orderBy,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _set.AsNoTracking();

        if (predicate is not null)
        {
            query = query.Where(predicate);
        }

        // COUNT runs before Skip/Take so it reflects all matches, not just this page.
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(orderBy)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>(items, totalCount, pageNumber, pageSize);
    }

    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        => _set.AnyAsync(predicate, cancellationToken);

    public async Task AddAsync(T entity, CancellationToken cancellationToken = default)
        => await _set.AddAsync(entity, cancellationToken);

    public void Update(T entity) => _set.Update(entity);

    public void Remove(T entity)
    {
        entity.IsDeleted = true;
        _set.Update(entity);
    }
}
