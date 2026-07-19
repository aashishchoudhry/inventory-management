# InventoryErp

An SME ERP / inventory management application built on .NET 10 using Clean Architecture.

> **Status: Core scope complete.** Authentication, products (full CRUD with search), customers
> (read-only), quotations with server-side GST calculation and PDF export, company settings with
> logo upload, global search, and a dashboard — all backed by SQL Server and covered by 185 tests.
>
> Read [known gaps](#known-gaps) before using this for anything real; the most significant is that
> tenant *scoping* is enforced in application services rather than by a global query filter. Progress
> log in [implementation-plan.md](implementation-plan.md).

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

# 1. Restore packages. Required before anything else — `dotnet ef` does not restore for you
#    and fails with NETSDK1004 on a fresh clone.
dotnet restore

# 2. Check the connection string (see below) before continuing.

# 3. Run. Pending migrations are applied and sample data seeded automatically on first start.
dotnet run --project src/InventoryErp.Web
```

Browse to the URL printed in the console and **sign in with the seeded account**:

| Email | Password |
| --- | --- |
| `admin@inventoryerp.local` | `Admin@123456` |

> There is no self-registration — the sign-up pages were deliberately removed, so the seeded
> account is the only way in. Development credentials only; see [Security](#security).

On first run this creates one company, 8 products, 7 customers, 3 roles and the admin user.
Seeding is skipped on later runs, so restarting never duplicates data.

### Connection string

`src/InventoryErp.Web/appsettings.json` → `ConnectionStrings:DefaultConnection`, which defaults to
the local default SQL Server instance using Windows authentication:

```
Server=.;Database=InventoryErp;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True
```

Adjust it if your instance differs — the provider is standard SQL Server, so switching instances
(or to Azure SQL) is a connection-string change only, with no code change.

### Applying migrations manually

Not required — the application migrates itself on startup. If you want to run them separately
(installing `dotnet-ef` first, per Prerequisites):

```bash
dotnet ef database update --project src/InventoryErp.Infrastructure --startup-project src/InventoryErp.Web
```

Full database documentation, including how to verify persistence across a restart, is in
[database/setup-notes.md](database/setup-notes.md).

## Build and test

```bash
dotnet build InventoryErp.slnx    # 0 warnings (warnings-as-errors is enabled)
dotnet test  InventoryErp.slnx    # 185 tests
```

The two mandatory acceptance tests can be run alone:

```bash
dotnet test --filter "FullyQualifiedName~Acceptance"
```

Tests use the EF in-memory provider and need no database. See [test-strategy.md](test-strategy.md)
for what that does **not** cover.

The solution file is `.slnx`, the XML format that is default in .NET 10. It works with the .NET CLI
and current Visual Studio / Rider, but not older tooling.

## Domain model

`Company`, `CompanySetting`, `Product`, `Customer`, `Quotation`, `QuotationLine` — all documented
with field-level detail in [data-model.md](data-model.md).

`ApplicationUser` / `ApplicationRole` are **not** domain entities. They derive from ASP.NET Identity
types and live in Infrastructure, so no ASP.NET dependency reaches Domain.

## Security

No API keys, tokens or passworded connection strings are committed. The connection string uses
Windows integrated authentication (`Trusted_Connection=True`), so it contains no credential.

Two things **are** committed deliberately, and must not reach a deployed environment:

| Item | Where | Why it is acceptable here |
| --- | --- | --- |
| Seed admin password `Admin@123456` | `SeedData.cs`, and documented in this README | A development fixture. There is no registration, so a known account is the only way to sign in for review |
| `TrustServerCertificate=True` | `appsettings.json` | Accepts a self-signed local SQL certificate. Should be removed where a valid certificate exists |

Before any real deployment: gate seeding to the Development environment or move the credential to
user secrets, remove `TrustServerCertificate`, move the connection string out of
`appsettings.json`, and revisit `RequireConfirmedAccount = false`.

## Known gaps

These are tracked deliberately rather than forgotten:

- **Tenant scoping has no database-level enforcement.** Reads and writes are both scoped by
  `CompanyId` and covered by tests — including the id-based paths, after a client-supplied
  `CompanyId` defect was found and fixed in review (`ai-prompts/debugging.md` #10). The database now
  enforces *referential* integrity on `CompanyId` (foreign keys added in `AddCompanyForeignKeys`),
  so a forged or orphan tenant id is rejected outright. But *scoping* between two real companies
  still rests on every service filtering its own queries; a global EF query filter would make that
  bug class structurally impossible.
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
