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
| Database provider | SQL Server (LocalDB for development) |
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
| `dotnet ef database update` | Schema applied to LocalDB |
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
