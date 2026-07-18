using System.Linq.Expressions;
using InventoryErp.Domain.Common;

namespace InventoryErp.Domain.Interfaces;

/// <summary>
/// Persistence abstraction for aggregate roots. Deliberately free of EF Core types so the
/// domain layer stays dependency-free.
/// </summary>
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> ListAsync(
        Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default);

    Task<bool> AnyAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default);

    Task AddAsync(T entity, CancellationToken cancellationToken = default);

    void Update(T entity);

    /// <summary>Soft-deletes the entity by setting <see cref="BaseEntity.IsDeleted"/>.</summary>
    void Remove(T entity);
}
