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

    /// <summary>
    /// Returns one page of matching rows plus the total match count. Paging and counting happen
    /// in the database — callers must not fetch everything and page in memory.
    /// </summary>
    /// <remarks>
    /// <paramref name="orderBy"/> is generic over the key type rather than
    /// <c>Expression&lt;Func&lt;T, object&gt;&gt;</c>, which would box value-typed keys and can
    /// fail to translate to SQL.
    /// </remarks>
    Task<PagedResult<T>> ListPagedAsync<TKey>(
        Expression<Func<T, bool>>? predicate,
        Expression<Func<T, TKey>> orderBy,
        int pageNumber,
        int pageSize,
        bool descending = false,
        CancellationToken cancellationToken = default);

    Task<bool> AnyAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts matching rows in the database, without materialising any of them.
    /// </summary>
    /// <remarks>
    /// Use this for totals rather than fetching a page and reading its length — that only ever
    /// counts as far as the page size, which silently understates once the data outgrows it.
    /// </remarks>
    Task<int> CountAsync(
        Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default);

    Task AddAsync(T entity, CancellationToken cancellationToken = default);

    void Update(T entity);

    /// <summary>Soft-deletes the entity by setting <see cref="BaseEntity.IsDeleted"/>.</summary>
    void Remove(T entity);
}
