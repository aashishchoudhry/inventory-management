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

## `IQuotationService`

`InventoryErp.Application/Interfaces/IQuotationService.cs`, implemented by
`InventoryErp.Application/Services/QuotationService.cs`. Service layer only — no controller or view
yet.

### `CreateQuotationAsync`

```csharp
Task<ServiceResult<QuotationDto>> CreateQuotationAsync(
    CreateQuotationRequest request,
    CancellationToken cancellationToken = default);
```

Creates a quotation and its lines in a single save, computing every monetary figure server-side.

#### Input — `CreateQuotationRequest`

| Field | Type | Rules |
| --- | --- | --- |
| `CompanyId` | `Guid` | Required; `Guid.Empty` → `ValidationFailed` |
| `CustomerId` | `Guid` | Required; must exist **within the company** |
| `QuotationDate` | `DateTime` | Determines the number's year segment |
| `ValidUntil` | `DateTime?` | Optional. Must not precede `QuotationDate` |
| `Notes` | `string?` | Optional |
| `Lines` | list | **At least one required** |

Per line — `CreateQuotationLineRequest`:

| Field | Type | Rules |
| --- | --- | --- |
| `ProductId` | `Guid` | Required; must exist within the company |
| `Quantity` | `int` | **> 0** |
| `UnitPrice` | `decimal` | ≥ 0 |
| `DiscountPercent` | `decimal` | 0–100 |
| `GstPercent` | `decimal` | ≥ 0 |

**No monetary totals are accepted from the caller.** `SubTotal`, `TaxAmount`, `DiscountAmount` and
`TotalAmount` are computed; a client cannot submit its own figures.

`UnitPrice` and `GstPercent` *are* taken from the caller rather than read from the product, so a
quotation can be issued at a negotiated price and keeps the figures it was issued with even after
the product changes.

#### Calculation

Per line, in this order:

```
gross    = round(quantity × unitPrice)
discount = round(gross × discountPercent / 100)
taxable  = gross − discount
tax      = round(taxable × gstPercent / 100)
lineTotal = taxable + tax
```

Header totals are sums of the already-rounded line figures:

```
SubTotal       = Σ gross
DiscountAmount = Σ discount
TaxAmount      = Σ tax
TotalAmount    = SubTotal − DiscountAmount + TaxAmount   ( = Σ lineTotal )
```

**GST is charged after discount, not on the gross.** This follows Indian GST practice, where tax is
levied on the transaction value after any discount shown on the invoice. Taxing the gross would
overstate tax on every discounted line — on a 1,000 line at 10% off and 18% GST, by 18.00.

**Rounding is half-away-from-zero to 2 decimal places**, applied as each figure is produced — not
.NET's default banker's rounding, which is not what invoices use. Because header totals sum
already-rounded lines, the stored header always reconciles exactly with the stored lines.

Worked example — qty 7 at 425.00, 12.5% discount, 12% GST:
gross 2975.00 → discount 371.88 (371.875 rounded up) → taxable 2603.12 → tax 312.37 → **2915.49**.

#### Quotation number

Format **`QT-{yyyy}-{NNNN}`**, e.g. `QT-2026-0001`. Sequential **per company, per calendar year**.

Chosen because it is human-readable and quotable over the phone; sorts chronologically as text;
the year segment keeps sequences short and resets annually, which matches how businesses file
documents; and per-company scoping matches the existing filtered unique index on
`(CompanyId, QuotationNumber)`.

The next value is derived from the **highest existing number**, not a row count — counting breaks
as soon as anything is deleted.

> **Known limitation.** Read-then-write is not atomic, so two concurrent creates could compute the
> same number. The filtered unique index is the real guarantee; the service retries up to five
> times and returns `Conflict` if it still cannot allocate. A database sequence would remove the
> race and is the right fix if creation ever becomes concurrent.
>
> Because the unique index excludes soft-deleted rows, a soft-deleted number can be reissued.
> Acceptable while nothing deletes quotations; revisit if that changes.

#### Outputs

| Status | When |
| --- | --- |
| `Success` | Created. `Data` is the `QuotationDto` including computed totals and all lines |
| `ValidationFailed` | Malformed request. `ValidationErrors` lists every problem, line-level ones prefixed `Line N:` so the caller can point at the offending row |
| `NotFound` | Customer or a referenced product does not exist in the company |
| `Conflict` | No unique quotation number could be allocated after retries |

Validation runs **before** any database write, and existence checks before any insert, so a rejected
request writes nothing.

Products belonging to another company read as `NotFound` rather than being usable — a tenant must
not be able to quote another tenant's catalogue by guessing an id.

`QuotationLineDto` also exposes `GrossAmount` and `DiscountAmount`, which are **computed, not
persisted** — the `QuotationLine` entity stores only `TaxAmount` and `TotalAmount`. They are
returned so a caller can render a line breakdown without recomputing it.

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
