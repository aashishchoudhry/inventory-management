using System.Linq.Expressions;
using InventoryErp.Domain.Common;
using InventoryErp.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InventoryErp.Infrastructure.Persistence.Repositories;

public class Repository<T> : IRepository<T> where T : BaseEntity
{
    private readonly InventoryErpDbContext _context;
    private readonly DbSet<T> _set;

    public Repository(InventoryErpDbContext context)
    {
        _context = context;
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
        bool descending = false,
        CancellationToken cancellationToken = default)
    {
        var query = _set.AsNoTracking();

        if (predicate is not null)
        {
            query = query.Where(predicate);
        }

        // COUNT runs before Skip/Take so it reflects all matches, not just this page.
        var totalCount = await query.CountAsync(cancellationToken);

        var ordered = descending ? query.OrderByDescending(orderBy) : query.OrderBy(orderBy);

        var items = await ordered
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>(items, totalCount, pageNumber, pageSize);
    }

    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        => _set.AnyAsync(predicate, cancellationToken);

    public async Task AddAsync(T entity, CancellationToken cancellationToken = default)
        => await _set.AddAsync(entity, cancellationToken);

    public void Update(T entity) => Attach(entity);

    public void Remove(T entity)
    {
        entity.IsDeleted = true;
        Attach(entity);
    }

    /// <summary>
    /// Marks an entity modified, tolerating the case where a different instance with the same key
    /// is already tracked.
    /// </summary>
    /// <remarks>
    /// Reads go through <c>AsNoTracking</c>, so callers hold detached copies. If the same scope
    /// previously inserted or loaded that row, a second instance with the same key would make
    /// <c>DbSet.Update</c> throw "cannot be tracked because another instance ... is already being
    /// tracked". Copying values onto the tracked instance keeps the change tracker consistent
    /// and makes repeated saves within one scope safe.
    /// </remarks>
    private void Attach(T entity)
    {
        var tracked = _set.Local.FirstOrDefault(e => e.Id == entity.Id);

        if (tracked is not null && !ReferenceEquals(tracked, entity))
        {
            _context.Entry(tracked).CurrentValues.SetValues(entity);
            return;
        }

        _set.Update(entity);
    }
}
