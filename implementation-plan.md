# Implementation Plan

A running log of work, newest entry appended at the bottom.

---

## Entry 1 — 2026-07-18 — Solution scaffold

**Goal:** Stand up the Clean Architecture solution skeleton with the dependency rule enforced by
project references, plus one vertical slice (Product) proving the wiring compiles, migrates and runs.

### Scope

| Item | Decision |
| --- | --- |
| Target framework | `net10.0` |
| Database provider | SQL Server (local default instance) |
| Scope | Full six-project skeleton + Product vertical slice |

### Work completed

1. **Solution and projects** — created `InventoryErp.slnx` and six projects under `src/` and `tests/`.
   `Directory.Build.props` centralises `TargetFramework`, `Nullable`, `ImplicitUsings` and
   `TreatWarningsAsErrors`, so the individual `.csproj` files stay minimal.

2. **Project references** — wired so dependencies point inward:
   - `Domain` → nothing
   - `Application` → Domain, Shared
   - `Infrastructure` → Application, Domain, Shared
   - `Web` → Application, Infrastructure, Shared
   - `Application.Tests` → Application, Domain, Shared, Infrastructure

3. **Shared** — `AppConstants`, `SettingKeys`, `Roles`.

4. **Domain** — `BaseEntity` (id + audit fields + soft-delete flag), `Product`, `ProductStatus`,
   `IRepository<T>`, `IUnitOfWork`. No EF or framework types.

5. **Application** — `ServiceResult` / `ServiceResult<T>` with `ResultStatus`, Product DTOs,
   `IProductService`, `ICurrentUser`, and a placeholder `IPdfGenerator`.

6. **Infrastructure** — `ApplicationUser` / `ApplicationRole` (Guid keys), `ApplicationDbContext`
   with soft-delete query filters and audit stamping, `ProductConfiguration`, `Repository<T>`,
   `UnitOfWork`, `ProductService`.

7. **Web** — DI wiring in `Program.cs`, `CurrentUser` reading the HTTP context, `ProductsController`
   translating `ResultStatus` into HTTP responses, and Razor views for the Product CRUD.

8. **Tests** — 13 xUnit tests covering the `ServiceResult` factories and `ProductService` behaviour
   (create, duplicate SKU conflict, blank SKU, not-found, update, soft delete).

### Verification

| Check | Result |
| --- | --- |
| `dotnet build InventoryErp.slnx` | Succeeded, 0 warnings (warnings-as-errors enabled) |
| `dotnet test InventoryErp.slnx` | 13 passed, 0 failed |
| `dotnet ef migrations add InitialCreate` | Migration generated |
| `dotnet ef database update` | Schema applied (initially to LocalDB; later repointed to `Server=.`) |
| Manual run | Registered a user, created a product, confirmed the reorder badge, confirmed a duplicate SKU renders a form error rather than throwing |
| Audit stamping | `CreatedBy` persisted as the signed-in user, confirmed via direct SQL query |
| Dependency rule | `InventoryErp.Domain.csproj` contains no project or package references |

### Decisions and deviations

- **Identity registration moved to Web.** `AddDefaultUI()` ships in the ASP.NET-only
  `Microsoft.AspNetCore.Identity.UI` package; registering Identity in Infrastructure would have
  forced a framework reference into that layer. `AddInfrastructure` handles DbContext, repositories
  and services; `Program.cs` handles Identity.
- **FluentAssertions removed.** v8 requires a paid commercial licence. Tests use plain xUnit asserts.
- **`CreateProductRequest` is not sealed**, because `UpdateProductRequest` derives from it.
- **SKU unique index is filtered** (`WHERE [IsDeleted] = 0`) so a soft-deleted SKU can be reused.
  This filter is SQL Server syntax and is ignored by the InMemory provider used in tests, so the
  duplicate-SKU guard in `ProductService` is what the tests actually exercise.

### Known gaps carried forward

- `IPdfGenerator` has no implementation; injecting it will fail until a PDF library is chosen.
- Roles in `Roles.All` are declared but never seeded, so `[Authorize(Roles = ...)]` would deny
  everyone. `ProductsController` currently uses a bare `[Authorize]`.
- No role seeding, no admin bootstrap user, no pagination on the product list.

---

## Entry 2 — 2026-07-18 — Domain entities: Company and CompanySetting

**Goal:** Begin the domain model with the tenant root and its settings store.

- `Company` — name, tagline, address fields, GST/PAN identifiers, contact details, logo path.
- `CompanySetting` — `CompanyId` / `Key` / `Value`, an untyped key/value store so new settings need
  no schema change. Well-known keys live in `SettingKeys` (Shared).

Both derive from `BaseEntity`, so they inherit `Guid` ids, audit fields and soft delete. Plain
properties only, no EF attributes.

**Decision — `Guid` over `int`:** `BaseEntity.Id` and `IRepository<T>.GetByIdAsync(Guid)` already fix
the key type. Deviating per entity would mean abandoning the base class or making it generic on key
type. Guid also allows ids to be assigned client-side without a database round-trip, which matters
when building an object graph before saving.

**Committed as** `9ff1009`.

---

## Entry 3 — 2026-07-18 — Domain entities: Customer, and Product rework

**Goal:** Add `Customer` and bring `Product` to the agreed field set.

`Product` already existed from the scaffold's vertical slice with a different shape, so this was a
rework rather than an addition. The conflict was surfaced and resolved before any code changed.

| Change | Detail |
| --- | --- |
| Added | `CompanyId`, `Barcode`, `GstPercent` |
| Renamed | `UnitPrice` → `SellingPrice`, `QuantityOnHand` → `CurrentStock` |
| Retained | `Description`, `ReorderLevel`, `Status` — so the reorder badge and lifecycle survive |

SKU uniqueness became **per tenant**: the index is `(CompanyId, Sku)` and the service duplicate check
filters by company. Two tenants may hold the same SKU. A test covers this.

Propagated through the whole vertical slice in one step to keep the build green — DTOs, EF
configuration, service, controller, four views and tests.

**Committed as** `8a79e63`.

---

## Entry 4 — 2026-07-18 — Domain entities: Quotation and QuotationLine

**Goal:** Complete the domain model for Core scope.

- `Quotation` — number, customer, dates, and stored monetary totals.
- `QuotationLine` — product, quantity, and per-line price, discount, GST and totals.

**Decision — totals are stored, not computed.** Each line copies `UnitPrice` and `GstPercent` from
the product at quoting time. A quotation is a commercial document and must keep the figures it was
issued with, even after prices or tax rates change. The cost is that stored totals can drift from
their inputs, so recalculation must live in one place in the application layer.

**Decision — `QuotationLine` has no `CompanyId`.** Its tenant comes from the parent quotation;
duplicating it would create two sources of truth that could disagree.

**Committed as** `f2c8849`.

---

## Entry 5 — 2026-07-18 — Infrastructure: DbContext and initial migration

**Goal:** Wire all six entities into EF Core and create the database.

### Work completed

1. **Renamed** `ApplicationDbContext` → `InventoryErpDbContext` across 8 files. A second context was
   considered and rejected: two contexts over one database means two migration histories and
   contested table ownership.

2. **Configuration classes** — `CompanyConfiguration`, `CompanySettingConfiguration`,
   `CustomerConfiguration`, `QuotationConfiguration`, `QuotationLineConfiguration`, joining the
   existing `ProductConfiguration`. All Fluent API, picked up by `ApplyConfigurationsFromAssembly`.
   No attributes on domain classes.

3. **Relationships**, configured with reference-less overloads since the domain has no navigation
   properties:

   | From | To | Behaviour | Why |
   | --- | --- | --- | --- |
   | `QuotationLine` | `Quotation` | Cascade | A line has no independent existence |
   | `QuotationLine` | `Product` | Restrict | A quoted product must not vanish under the line |
   | `Quotation` | `Customer` | Restrict | Deleting a customer must not destroy their quotations |

   Restrict overrides EF's default of cascade for required relationships, which would have made
   deleting a customer silently destroy every quotation issued to them.

4. **Migration reset.** The scaffold's `InitialCreate` was stale — generated before the Product
   rework and the other five entities. It was deleted along with its database, and a fresh
   `20260718074523_InitialCreate` generated covering all six domain tables plus eight Identity
   tables.

5. **Database instance switched.** The schema was first applied to `(localdb)\MSSQLLocalDB`, then
   repointed to the local default instance `Server=.` when the connection string changed. Since the
   provider is identical this needed no code or migration changes; the orphaned LocalDB database was
   dropped.

### Verification

| Check | Result |
| --- | --- |
| `dotnet build` | Clean, 0 warnings |
| `dotnet test` | 14/14 passing |
| `dotnet ef dbcontext info` | Model loads under the SQL Server provider |
| Tables created | 14 (6 domain + 8 Identity) |
| Cascade delete | Deleting a quotation removed its line (1 → 0) |
| Restrict delete | Deleting a customer holding a quotation was blocked by the FK |
| Nullable unique index | Three customers with null `Code` coexisted in one company |

### Known gaps carried forward

- **Cascade does not fire on soft delete.** `Repository<T>.Remove` issues an `UPDATE`, never a
  `DELETE`, so soft-deleting a quotation would leave its lines visible and orphaned. Not yet live —
  nothing deletes quotations — but any delete feature must cascade the soft delete in application code.
- **Tenant isolation is not enforced.** `CompanyId` is a column, not a boundary. Reads are not
  filtered by tenant and `CreateProductRequest.CompanyId` is a user-editable form field. Needs an
  `ICurrentTenant` abstraction plus a global query filter.
- **No seed data.** The database is empty, so no `Company` row exists to own tenant-scoped records.
- **No `Quotation` status/lifecycle field** (draft / sent / accepted / expired).
- **`Product.Barcode` uniqueness undecided.**
- Application services, DTOs and UI exist only for `Product`.

---
