# Database Setup Notes

## Database choice

**SQL Server 2022**, local default instance.

| Item | Value |
| --- | --- |
| Provider | `Microsoft.EntityFrameworkCore.SqlServer` 10.0.10 |
| Development instance | `.` (local default instance, SQL Server 2022) |
| Database name | `InventoryErp` |
| Connection string key | `ConnectionStrings:DefaultConnection` in `appsettings.json` |
| Authentication | Windows integrated (`Trusted_Connection=True`) |

### Connection string

```
Server=.;Database=InventoryErp;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True
```

`TrustServerCertificate=True` is acceptable for local development against a self-signed certificate.
It should be removed in any deployed environment, where a valid certificate is expected instead.

> **Note on instance choice.** The project was initially scaffolded against
> `(localdb)\MSSQLLocalDB` and the first migration was applied there. It has since been repointed at
> the full local instance, the schema recreated on it, and the orphaned LocalDB database dropped.
> Because the provider is the same, the switch was a connection-string change only — no code or
> migration changes were needed. The same applies to moving to a hosted instance or Azure SQL later.

### Which instance is being used

`dotnet ef` reads the connection string from the startup project's `appsettings.json`, so migration
commands always target whatever `DefaultConnection` currently points at. After changing it, verify
where the schema actually landed:

```bash
sqlcmd -S "." -I -Q "SELECT name FROM sys.databases WHERE name='InventoryErp';"
```

## DbContext

`InventoryErpDbContext` lives in `InventoryErp.Infrastructure/Persistence`. It derives from
`IdentityDbContext<ApplicationUser, ApplicationRole, Guid>`, so Identity's tables and the domain
tables share one context and one migration history.

Beyond mapping, it provides two cross-cutting behaviours:

- **Soft delete** — a global query filter of `!IsDeleted` is applied to every `BaseEntity`, so
  deleted rows stay in the database but are invisible to normal queries. Use `IgnoreQueryFilters()`
  to see them.
- **Audit stamping** — `CreatedAtUtc` / `CreatedBy` and `ModifiedAtUtc` / `ModifiedBy` are set in
  `SaveChangesAsync`, taking the username from the `ICurrentUser` abstraction.

### Entities wired so far

| Entity | DbSet | Configuration | Status |
| --- | --- | --- | --- |
| `Company` | `Companies` | `CompanyConfiguration` | Done |
| `CompanySetting` | `CompanySettings` | `CompanySettingConfiguration` | Done |
| `Product` | `Products` | `ProductConfiguration` | Done (from the initial scaffold) |
| `Customer` | `Customers` | `CustomerConfiguration` | Done |
| `Quotation` | `Quotations` | `QuotationConfiguration` | Done |
| `QuotationLine` | `QuotationLines` | `QuotationLineConfiguration` | Done |

**All six domain entities are wired**, alongside the eight ASP.NET Identity tables.

### Tenant-scoped indexes

Tenant-owned entities carry a non-unique index on `CompanyId` for tenant-scoped queries, plus a
filtered unique index for their per-company business key:

| Entity | Unique index | Filter |
| --- | --- | --- |
| `CompanySetting` | `(CompanyId, Key)` | `IsDeleted = 0` |
| `Product` | `(CompanyId, Sku)` | `IsDeleted = 0` |
| `Customer` | `(CompanyId, Code)` | `IsDeleted = 0 AND Code IS NOT NULL` |
| `Quotation` | `(CompanyId, QuotationNumber)` | `IsDeleted = 0` |

The extra `IS NOT NULL` clause on `Customer` is required because `Code` is optional — see the note
in `data-model.md`.

> **Filtered indexes require `QUOTED_IDENTIFIER ON`.** Ad-hoc `sqlcmd` sessions default it off and
> will fail any INSERT with *"INSERT failed because the following SET options have incorrect
> settings: 'QUOTED_IDENTIFIER'"*. Pass `sqlcmd -I` to avoid this. The application is unaffected —
> the .NET SQL client sets it on by default.

### Relationships

| From | To | Delete behaviour |
| --- | --- | --- |
| `QuotationLine` | `Quotation` | Cascade |
| `QuotationLine` | `Product` | Restrict |
| `Quotation` | `Customer` | Restrict |

Configured via Fluent API with no navigation properties on the domain entities. Note that the
cascade is a database-level `ON DELETE CASCADE` and does **not** fire on soft delete — see
`data-model.md`.

## Migrations

Migration files live in `InventoryErp.Infrastructure/Persistence/Migrations`.

```bash
dotnet ef migrations add <Name> -p src/InventoryErp.Infrastructure -s src/InventoryErp.Web -o Persistence/Migrations
dotnet ef database update      -p src/InventoryErp.Infrastructure -s src/InventoryErp.Web
```

The startup project is `InventoryErp.Web` because it holds the connection string and the DI wiring.

### Convention: one migration per new entity

**From this point on, every new database entity gets its own dedicated migration.** Do not bundle
several entities into a single migration, and never regenerate or amend an existing migration to
absorb a new change.

The migration history is the record of how the schema evolved. One migration per entity keeps each
change independently reviewable, revertible, and traceable to the step that introduced it.

Name migrations after what they add — `AddSupplier`, `AddPurchaseOrder` — so `dotnet ef migrations
list` reads as a changelog.

```bash
# 1. Wire the entity: DbSet on InventoryErpDbContext + an IEntityTypeConfiguration class
# 2. Generate its migration
dotnet ef migrations add Add<EntityName> \
  --project src/InventoryErp.Infrastructure \
  --startup-project src/InventoryErp.Web \
  --output-dir Persistence/Migrations

# 3. Apply it
dotnet ef database update \
  --project src/InventoryErp.Infrastructure \
  --startup-project src/InventoryErp.Web
```

Always inspect the generated `Up()` before applying. An empty `Up()` means EF found no model
changes — usually the entity was not actually wired into the context.

> **The drop-and-recreate shortcut is retired.** It was used once early on, when a stale
> `InitialCreate` predated the `Product` rework and nothing had shipped. The database now has a
> history worth preserving; use additive migrations from here on.

### Creating and applying the initial migration

Run from the repository root. `InventoryErp.Infrastructure` holds the migrations,
`InventoryErp.Web` is the startup project because it holds the connection string and DI wiring.

```bash
# Generate the migration
dotnet ef migrations add InitialCreate \
  --project src/InventoryErp.Infrastructure \
  --startup-project src/InventoryErp.Web \
  --output-dir Persistence/Migrations

# Apply it to the database
dotnet ef database update \
  --project src/InventoryErp.Infrastructure \
  --startup-project src/InventoryErp.Web
```

The short forms `-p` and `-s` are equivalent to `--project` and `--startup-project`.

`dotnet-ef` is a global tool. If `dotnet ef` is not found:

```bash
dotnet tool install --global dotnet-ef
```

### Current migration state

| Migration | Status |
| --- | --- |
| `20260718074523_InitialCreate` | Applied |

Creates all six domain tables plus the seven ASP.NET Identity tables. It supersedes an earlier
`InitialCreate` that was generated before the `Product` rework and the remaining entities existed;
that migration and its database were deleted rather than patched, since nothing had shipped and the
database held only throwaway verification data.

### Identity tables came in the same migration, not a second one

`InventoryErpDbContext` derives from `IdentityDbContext<ApplicationUser, ApplicationRole, Guid>`, so
the Identity schema is part of the same model and the same migration history. `InitialCreate`
therefore creates both the domain tables and:

`AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, `AspNetUserClaims`, `AspNetRoleClaims`,
`AspNetUserLogins`, `AspNetUserTokens`.

A separate `AddIdentity` migration was attempted and **generated empty** — EF found no model changes,
confirming Identity was already fully mapped. It was removed rather than committed, since an empty
migration adds nothing but noise.

`AspNetUsers` carries two custom columns from `ApplicationUser`: `FullName` (`nvarchar`) and
`IsActive` (`bit`). Keys are `uniqueidentifier`, not the Identity default of `nvarchar(450)`, because
`ApplicationUser` derives from `IdentityUser<Guid>` to match the `Guid` convention used by every
domain entity.

### Resetting the development database

Safe while the schema is still in flux and no real data exists:

```bash
dotnet ef database drop --project src/InventoryErp.Infrastructure --startup-project src/InventoryErp.Web --force
dotnet ef database update --project src/InventoryErp.Infrastructure --startup-project src/InventoryErp.Web
```

Once the application has real data, replace this with an additive migration instead.

## Seed data

Sample data is written on first application launch by `DatabaseSeeder`
(`InventoryErp.Infrastructure/Persistence/Seeding`). `Program.cs` applies pending migrations, then
calls the seeder inside a service scope.

**The seeder is idempotent.** It checks whether any `Company` exists — the tenant root — and returns
immediately if one does. Restarting the application never duplicates data or modifies existing rows.
Roles are checked separately so a role added later is still created on an already-seeded database.

### Seeded login credentials

| Field | Value |
| --- | --- |
| Email / username | `admin@inventoryerp.local` |
| Password | `Admin@123456` |
| Role | `Admin` |
| Full name | System Administrator |

> **Development only.** These credentials are hard-coded in `SeedData.cs` and committed to the
> repository. They must never exist in a deployed environment. Before any real deployment, either
> gate seeding to the Development environment or move the credentials to user secrets.

### What gets seeded

| Entity | Count | Notes |
| --- | --- | --- |
| `Company` | 1 | Sharma Industrial Supplies Pvt Ltd — all fields populated, including a valid-format GSTIN and PAN |
| `ApplicationRole` | 3 | `Admin`, `Manager`, `StoreKeeper` — from `Roles.All` |
| `ApplicationUser` | 1 | The admin above, email pre-confirmed so login works immediately |
| `Product` | 8 | Varied GST slabs (5/12/18/28%), prices from ₹6.75 to ₹8,499, three below reorder level, one `Discontinued`, one with a null barcode |
| `Customer` | 7 | Across Gujarat, Telangana, Kerala, Punjab, Rajasthan, West Bengal, Maharashtra. One deliberately has a null `Code` |
| `CompanySetting` | 0 | Not seeded |
| `Quotation` / `QuotationLine` | 0 | Not seeded |

Audit fields on seeded rows show `CreatedBy = "system"`, because there is no HTTP context during
startup. `InventoryErpDbContext` falls back to `"system"` when `ICurrentUser` yields no username.

### Re-seeding

Seeding only runs against a database with no company. To force a re-seed, delete the existing data
(or drop and recreate the database) and restart the application.

## Verifying persistence after a restart

The point of this check is to confirm data survives independently of the application process — that
it is genuinely in SQL Server, not in memory.

**1. Confirm the schema exists**

```bash
sqlcmd -S "." -d InventoryErp -I -Q "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE' ORDER BY TABLE_NAME;"
```

Expect 14 tables: `__EFMigrationsHistory`, seven `AspNet*` Identity tables, and `Companies`,
`CompanySettings`, `Customers`, `Products`, `Quotations`, `QuotationLines`.

**2. Confirm the migration is recorded**

```bash
sqlcmd -S "." -d InventoryErp -I -Q "SELECT MigrationId FROM __EFMigrationsHistory;"
```

**3. Write data through the application**

```bash
dotnet run --project src/InventoryErp.Web
```

Register a user, then create a product at `/Products/Create`.

**4. Stop the application** with `Ctrl+C`, so the process and its memory are gone.

**5. Confirm the rows are still in the database**

```bash
sqlcmd -S "." -d InventoryErp -I -Q "SELECT Sku, Name, CreatedBy FROM Products;"
sqlcmd -S "." -d InventoryErp -I -Q "SELECT UserName FROM AspNetUsers;"
```

`CreatedBy` should hold the username of whoever was signed in, which also confirms audit stamping
is wired through the HTTP context.

**6. Restart the application** and confirm the product still appears at `/Products` and that the
same login still works.

SQL Server runs as a persistent Windows service, so the database is available without the app
running.
