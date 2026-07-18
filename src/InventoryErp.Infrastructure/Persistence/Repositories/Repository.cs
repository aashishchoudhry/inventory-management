using System.Linq.Expressions;
using InventoryErp.Domain.Common;
using InventoryErp.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InventoryErp.Infrastructure.Persistence.Repositories;

public class Repository<T> : IRepository<T> where T : BaseEntity
{
    private readonly DbSet<T> _set;

    public Repository(ApplicationDbContext context)
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
