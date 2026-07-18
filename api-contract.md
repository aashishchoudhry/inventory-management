# API Contract

> There is no HTTP/REST API. `InventoryErp.Web` is a Razor MVC application returning HTML.
> This document specifies the **application service contracts** in `InventoryErp.Application`,
> which are the internal API the web layer consumes.

## Conventions

Every method returns `ServiceResult<T>` (or `ServiceResult`) rather than throwing for expected
failures. Callers inspect `IsSuccess` and `Status`; exceptions are reserved for unexpected faults.

| `ResultStatus` | Meaning | Web layer maps to |
| --- | --- | --- |
| `Success` | Completed. For list methods, may contain zero items | 200 / render view |
| `NotFound` | No entity with that id | 404 |
| `ValidationFailed` | Caller supplied invalid arguments; see `ValidationErrors` | Redisplay with errors |
| `Conflict` | Violates a uniqueness rule | Redisplay with message |
| `Unauthorized` | Caller not permitted | 403 |
| `Error` | Unexpected failure | Problem response |

**An empty result is `Success`, not `NotFound`.** "No products match your search" is a valid answer
to a valid question. `NotFound` is reserved for a lookup by id that does not exist.

---

## `IProductService`

`InventoryErp.Application/Interfaces/IProductService.cs`, implemented by
`InventoryErp.Application/Services/ProductService.cs`. The implementation depends only on
`IUnitOfWork` / `IRepository<T>` from Domain — **no EF Core type reaches this layer**.

### `GetAllAsync`

```csharp
Task<ServiceResult<PagedResult<ProductDto>>> GetAllAsync(
    Guid companyId,
    int pageNumber = 1,
    int pageSize = 20,
    CancellationToken cancellationToken = default);
```

One page of products for a company, ordered by name ascending. Delegates to `SearchAsync` with a
null keyword — listing is search with no filter, so there is one code path, not two.

| Input | Rules |
| --- | --- |
| `companyId` | Required. `Guid.Empty` → `ValidationFailed` |
| `pageNumber` | 1-based. `< 1` → `ValidationFailed` |
| `pageSize` | `< 1` or `> 200` → `ValidationFailed` |

**Returns:** `Success` with `PagedResult<ProductDto>`. Results are scoped to `companyId`;
soft-deleted products are excluded by the global query filter. A page beyond the last returns zero
items with `TotalCount` still reflecting the full match count.

### `SearchAsync`

```csharp
Task<ServiceResult<PagedResult<ProductDto>>> SearchAsync(
    Guid companyId,
    string? keyword,
    int pageNumber = 1,
    int pageSize = 20,
    CancellationToken cancellationToken = default);
```

Same as `GetAllAsync`, filtered by keyword.

| Input | Rules |
| --- | --- |
| `companyId` | Required, as above |
| `keyword` | Optional. Trimmed. Null/empty/whitespace returns **all** products for the company |
| `pageNumber`, `pageSize` | As above |

**Matching:** case-insensitive *contains* against **`Name`**, **`Sku`**, or **`Barcode`** — any one
matching includes the product. Products with a null `Barcode` are handled safely.

Case-insensitivity is explicit (`ToLower()` on both sides), not inherited from the database
collation. SQL Server's default collation happens to be case-insensitive, but that is a per-database
setting; relying on it would make behaviour differ between environments and providers — and it did:
a unit test on the in-memory provider caught the search being case-sensitive. The cost is that
`LOWER()` prevents an index seek, acceptable at current scale and the first thing to revisit if
search becomes slow.

**Returns:** `Success` with a possibly-empty page. No match is **not** an error.

### Other methods

| Method | Purpose |
| --- | --- |
| `GetByIdAsync(Guid id)` | Single product, or `NotFound` |
| `CreateAsync(CreateProductRequest)` | `Conflict` on duplicate SKU within the company; `ValidationFailed` on blank SKU |
| `UpdateAsync(UpdateProductRequest)` | `NotFound` for unknown id; `Conflict` on duplicate SKU |
| `DeleteAsync(Guid id)` | Soft delete. `NotFound` for unknown id |

> Create, update and delete are **Stretch scope**. They were built during the initial scaffold and
> are retained because they work and are covered by tests; they are not part of the Core
> list-and-search feature.

---

## `ICustomerService`

`InventoryErp.Application/Interfaces/ICustomerService.cs`, implemented by
`InventoryErp.Application/Services/CustomerService.cs`. Same constraints as `IProductService` —
depends only on `IUnitOfWork` / `IRepository<T>`, no EF Core type reaches this layer.

### `GetAllAsync`

```csharp
Task<ServiceResult<PagedResult<CustomerDto>>> GetAllAsync(
    Guid companyId,
    int pageNumber = 1,
    int pageSize = 20,
    CancellationToken cancellationToken = default);
```

One page of customers for a company, ordered by name ascending.

| Input | Rules |
| --- | --- |
| `companyId` | Required. `Guid.Empty` → `ValidationFailed` |
| `pageNumber` | 1-based. `< 1` → `ValidationFailed` |
| `pageSize` | `< 1` or `> 200` → `ValidationFailed` |

**Returns:** `Success` with `PagedResult<CustomerDto>`. Scoped to `companyId`; soft-deleted
customers are excluded by the global query filter. A page beyond the last returns zero items with
`TotalCount` still reflecting the full count. An empty list is `Success`, not `NotFound`.

**`CustomerDto`:** `Id`, `CompanyId`, `Name`, `Code`, `Mobile`, `City`, `State`, `Address`.
All except `Id`, `CompanyId` and `Name` are nullable — `Code` in particular, since the seeded
"Walk-in / Counter Sales" customer deliberately has none.

> **Read-only by design.** There is no create, update or delete: customer maintenance is Stretch
> scope. The service exposes exactly one method rather than stubs that throw.

---

## `ICurrentCompanyProvider`

```csharp
Task<Guid?> GetCompanyIdAsync(CancellationToken cancellationToken = default);
```

Supplies the tenant for the current request. Returns null when no company exists.

> **Interim implementation.** There is no tenant context — nothing on `ApplicationUser` links a user
> to a company — so the implementation resolves the single seeded company and caches it per request.
> Correct while exactly one company exists; wrong the moment a second is added. The interface exists
> so callers depend on the concept rather than the shortcut: when a company claim is added, only the
> implementation changes.

---

## `PagedResult<T>`

`InventoryErp.Domain/Common/PagedResult.cs`. Lives in Domain because `IRepository<T>` returns it.

| Member | Notes |
| --- | --- |
| `Items` | The current page |
| `TotalCount` | **All** matching rows, not just this page |
| `PageNumber`, `PageSize` | As requested |
| `TotalPages`, `HasPreviousPage`, `HasNextPage` | Derived |
| `Map<TOut>(selector)` | Projects items, preserving paging metadata — used for entity → DTO |

Paging and counting happen **in the database** via `IRepository<T>.ListPagedAsync`. The `COUNT` runs
before `Skip`/`Take`, so `TotalCount` is the full match count rather than the page size.

`ListPagedAsync` takes `Expression<Func<T, TKey>> orderBy` generic over the key type rather than
`Expression<Func<T, object>>`, which would box value-typed keys and can fail to translate to SQL.
