# InventoryErp

An SME ERP / inventory management application built on .NET 10 using Clean Architecture.

> **Status: in development.** The domain model and persistence layer are complete; application
> services and UI exist only for `Product`. See [implementation-plan.md](implementation-plan.md) for
> progress and [known gaps](#known-gaps).

## Architecture

Six projects, with the dependency rule enforced by project references rather than convention.
Dependencies point inward, and `InventoryErp.Domain` has no references at all.

```
InventoryErp.Domain          no dependencies
      ↑
InventoryErp.Application     → Domain, Shared
      ↑
InventoryErp.Infrastructure  → Application, Domain, Shared
      ↑
InventoryErp.Web             → Application, Infrastructure, Shared
```

| Project | Responsibility |
| --- | --- |
| `InventoryErp.Domain` | Entities, enums, repository interfaces. No dependencies. |
| `InventoryErp.Application` | DTOs, service interfaces, the `ServiceResult<T>` pattern |
| `InventoryErp.Infrastructure` | EF Core, ASP.NET Identity, service implementations |
| `InventoryErp.Shared` | Constants and settings keys |
| `InventoryErp.Web` | Razor MVC frontend and composition root |
| `InventoryErp.Application.Tests` | xUnit test suite |

`Web` references `Infrastructure` as a deliberate composition-root concession — it needs
`AddInfrastructure()` and Identity's EF stores. Controllers depend only on Application interfaces.

### Key patterns

- **`ServiceResult<T>` instead of exceptions.** Application services return a result carrying a
  `ResultStatus` (`Success`, `NotFound`, `ValidationFailed`, `Conflict`, `Unauthorized`, `Error`).
  Exceptions are reserved for genuinely unexpected faults. The web layer maps statuses onto HTTP
  responses. See [design-notes.md](design-notes.md).
- **Fluent API only.** No EF attributes on domain classes, so Domain stays dependency-free.
- **Soft delete.** `BaseEntity.IsDeleted` plus a global query filter; deleted rows stay auditable.
- **Audit stamping.** `CreatedBy` / `ModifiedBy` set in `SaveChangesAsync` from `ICurrentUser`.

## Prerequisites

| Requirement | Version used |
| --- | --- |
| .NET SDK | 10.0.302 |
| SQL Server | 2022, local default instance (`.`) |
| `dotnet-ef` | 10.0.10 (`dotnet tool install --global dotnet-ef`) |

## Getting started

```bash
git clone <repo-url>
cd inventory-management

# Create the database
dotnet ef database update --project src/InventoryErp.Infrastructure --startup-project src/InventoryErp.Web

# Run
dotnet run --project src/InventoryErp.Web
```

Then register a user and browse to `/Products`.

The connection string lives in `src/InventoryErp.Web/appsettings.json` under
`ConnectionStrings:DefaultConnection`. Adjust it if your SQL Server instance differs — the provider
is standard SQL Server, so switching instances is a connection-string change only.

Full database documentation, including how to verify persistence across a restart, is in
[database/setup-notes.md](database/setup-notes.md).

## Build and test

```bash
dotnet build InventoryErp.slnx    # 0 warnings (warnings-as-errors is enabled)
dotnet test  InventoryErp.slnx    # 14 tests
```

The solution file is `.slnx`, the XML format that is default in .NET 10. It works with the .NET CLI
and current Visual Studio / Rider, but not older tooling.

## Domain model

`Company`, `CompanySetting`, `Product`, `Customer`, `Quotation`, `QuotationLine` — all documented
with field-level detail in [data-model.md](data-model.md).

`ApplicationUser` / `ApplicationRole` are **not** domain entities. They derive from ASP.NET Identity
types and live in Infrastructure, so no ASP.NET dependency reaches Domain.

## Known gaps

These are tracked deliberately rather than forgotten:

- **Tenant isolation is enforced by convention, not by the database.** Services scope every query
  by `CompanyId`, but there is no global query filter, and `CompanyId` is still a user-editable
  field on the product create form. A caller that forgets to pass it, or a user who edits it, is
  not stopped. Needs an ambient tenant context plus a query filter.
- **`ICurrentCompanyProvider` resolves the single seeded company.** Correct while one company
  exists; wrong the moment a second is added.
- **Cascade delete does not fire on soft delete.** The database cascade only triggers on a hard
  `DELETE`, which the application never issues — so soft-deleting a quotation would leave its lines
  visible and orphaned.
- **Roles are seeded but unused.** No controller applies `[Authorize(Roles = ...)]`; every
  authenticated user has full access.
- **Quotation numbering has a race.** Read-then-write is not atomic; the filtered unique index is
  the real guarantee, with five retries before returning `Conflict`.
- **No edit or delete for quotations**, and no UI for company records beyond Settings.
- **Quotations have no status lifecycle** (draft / sent / accepted); the detail view infers
  "Expired" from `ValidUntil` alone.
- **QuestPDF's Community licence** is free only below a revenue threshold — revisit if that changes.

## Repository layout

| Path | Contents |
| --- | --- |
| `src/`, `tests/` | Source and tests |
| `database/` | Setup notes, schema/migration and seed-data folders |
| `ai-prompts/` | Prompts given to AI tools, by phase |
| `tool-specific/` | Tool-specific workflow files |
| `*.md` (root) | Planning, design, testing and review documentation |
