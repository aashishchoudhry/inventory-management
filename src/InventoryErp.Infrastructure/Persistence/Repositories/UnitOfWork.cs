using System.Collections.Concurrent;
using InventoryErp.Domain.Common;
using InventoryErp.Domain.Interfaces;

namespace InventoryErp.Infrastructure.Persistence.Repositories;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;
    private readonly ConcurrentDictionary<Type, object> _repositories = new();

    public UnitOfWork(ApplicationDbContext context)
    {
        _context = context;
    }

    public IRepository<T> Repository<T>() where T : BaseEntity
        => (IRepository<T>)_repositories.GetOrAdd(typeof(T), _ => new Repository<T>(_context));

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);
}
