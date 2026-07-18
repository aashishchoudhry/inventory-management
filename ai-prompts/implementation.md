# Implementation Prompts

A log of prompts given to AI tools during implementation, what the tool did, and what was
accepted, changed or rejected.

---

## 2026-07-18 — Infrastructure: EF Core setup and DbContext

**Tool:** Claude Code (Opus 4.8)

### Prompt

> Now let's start Infrastructure. Add the EF Core 10 SQL Server packages to InventoryErp.Infrastructure,
> and create an InventoryErpDbContext there with just Company and CompanySetting for now — I'll add
> the rest of the entities in the next steps.
>
> Use Fluent API in OnModelCreating for configuration (required fields, max lengths where it makes
> sense) — no attributes on the Domain classes themselves. Don't wire ASP.NET Identity yet, that's a
> separate step.
>
> Then:
> - Update database/setup-notes.md with the database choice (SQL Server LocalDB) and note that
>   DbContext setup has started with Company/CompanySetting
> - Update design-notes.md's Database Design section with a short note on the Fluent API approach and
>   why (keeps Domain clean of EF Core dependencies)
> - Update ai-prompts/implementation.md with this prompt, a summary of what Claude Code did, and what
>   you accepted/changed/rejected
>
> Don't run a migration yet — we'll do that once more entities are wired in.

### What Claude Code did

Before making changes, it checked the existing state and reported three conflicts with the request:

1. **The EF Core packages were already installed** — `SqlServer`, `Design` and
   `Identity.EntityFrameworkCore`, all 10.0.10, added during the initial scaffold. No action needed.
2. **A DbContext already existed.** `ApplicationDbContext` was already present as an
   `IdentityDbContext<ApplicationUser, ApplicationRole, Guid>` with a `Products` DbSet, soft-delete
   query filters and audit stamping, referenced by `Repository<T>`, `UnitOfWork`, `AddInfrastructure`
   and Identity's `AddEntityFrameworkStores`, with an applied migration.
3. **Identity was already wired** to that context, and the login flow had been verified working.

It stopped and asked how to proceed rather than creating a second DbContext over the same database.

After the decision, it:

- Renamed `ApplicationDbContext` → `InventoryErpDbContext`, updating all 8 referencing files and
  renaming the model snapshot file to match.
- Added `Companies` and `CompanySettings` DbSets.
- Created `CompanyConfiguration` and `CompanySettingConfiguration` as `IEntityTypeConfiguration<T>`
  classes, picked up by the existing `ApplyConfigurationsFromAssembly` call in `OnModelCreating`.
- Verified: build clean with 0 warnings, 14/14 tests passing, and `dotnet ef dbcontext info`
  confirming the model loads under the SQL Server provider.
- Wrote `database/setup-notes.md` and the Database Design section of `design-notes.md`.

### Accepted

- **Renaming the existing context** rather than creating a parallel one. Two contexts over one
  database would have meant two migration histories and contested table ownership.
- **`IEntityTypeConfiguration` classes** over inline `builder.Entity<T>()` calls in `OnModelCreating`,
  matching the existing `ProductConfiguration` pattern. This still satisfies "Fluent API, no
  attributes" — `ApplyConfigurationsFromAssembly` is itself called from `OnModelCreating`.
- **Domain-specific max lengths** proposed by the tool: GSTIN capped at 15 and PAN at 10, matching
  the fixed statutory formats, rather than a generic default.
- **A filtered unique index on `(CompanyId, Key)`** for `CompanySetting`, preventing duplicate
  setting keys within a company while allowing a soft-deleted key to be re-added.
- **`CompanySetting.Value` left uncapped**, since settings may hold anything from a boolean flag to
  a block of PDF footer text.

### Changed from the original request

- **"Create an InventoryErpDbContext"** became **rename the existing context**. Taken literally the
  request would have produced a second DbContext alongside `ApplicationDbContext`.
- **"Don't wire ASP.NET Identity yet"** could not be honoured. Identity was wired during the initial
  scaffold and the login flow depends on it; unwiring it would have broken a working feature to
  reach a state the next step would immediately undo. `InventoryErpDbContext` therefore still derives
  from `IdentityDbContext`.
- **"Add the EF Core packages"** was a no-op — they were already present.
- **"Fluent API in OnModelCreating"** was interpreted as "Fluent API rather than attributes", with
  the configuration living in per-entity classes invoked from `OnModelCreating`.

### Rejected

- Nothing was rejected outright. The instruction to defer Identity was deliberately not followed, for
  the reason given above, and was flagged rather than silently ignored.

### Open items after this step

- The migration is stale: it predates both the `Product` rework and these two new entities, so the
  local database no longer matches the model. A fresh migration is deferred until the remaining
  entities are wired in.
- Tenant isolation is still enforced only in service logic — no ambient tenant context, no global
  `CompanyId` query filter.

---

## 2026-07-18 — Infrastructure: Product and Customer in the DbContext

**Tool:** Claude Code (Opus 4.8)

### Prompt

> Now add Product and Customer to InventoryErpDbContext — same pattern as Company/CompanySetting,
> Fluent API config in OnModelCreating (required fields, max lengths, no attributes on Domain).
>
> Then:
> - Update database/setup-notes.md noting Product/Customer are now in the DbContext
> - Update data-model.md if the Fluent API config revealed anything worth noting about these two
>   entities (e.g. a field you had to make optional/required that wasn't obvious from the entity alone)
> - Update ai-prompts/implementation.md with this prompt, what Claude Code did, and your
>   accept/change/reject
>
> Still no migration yet — next step adds Quotation/QuotationLine, then we migrate everything together.

### What Claude Code did

Checked the DbContext first and found **`Product` was already fully wired** — its `Products` DbSet
and `ProductConfiguration` were created during the initial scaffold and updated during the
multi-tenancy rework. Only `Customer` was actually missing, so it reported this rather than
re-creating existing configuration.

Then:

- Added the `Customers` DbSet.
- Created `CustomerConfiguration` with required `Name` (max 200) and max lengths on the optional
  fields (`Code` 50, `Mobile` 20, `City`/`State` 100, `Address` 500).
- Added a non-unique index on `CompanyId`, matching `ProductConfiguration`.
- Added a filtered unique index on `(CompanyId, Code)` with an `IS NOT NULL` clause.
- Verified: build clean with 0 warnings, 14/14 tests passing, `dotnet ef dbcontext info` loads the
  model under the SQL Server provider. No migration generated, as instructed.
- Updated `database/setup-notes.md` and `data-model.md`.

### Accepted

- **Reporting that `Product` was already done** instead of duplicating work or silently overwriting
  a configuration that had been deliberately updated for multi-tenancy.
- **The `Code IS NOT NULL` index filter.** SQL Server treats NULLs as equal for uniqueness, so a
  plain unique index on `(CompanyId, Code)` would have allowed only one code-less customer per
  company. This is exactly the kind of non-obvious constraint the prompt asked to surface, and it is
  now documented in `data-model.md`.
- **Max lengths chosen per field semantics** rather than a uniform default — a mobile number and a
  street address do not warrant the same column width.

### Changed from the original request

- **"Add Product and Customer"** became **add Customer only**, because `Product` was already wired.
- **"Fluent API config in OnModelCreating"** again means a per-entity `IEntityTypeConfiguration`
  class picked up by `ApplyConfigurationsFromAssembly`, consistent with the previous step and with
  `ProductConfiguration`.

### Rejected

- **Did not add a unique index on `Product.Barcode`.** Barcodes arguably should be unique per company
  and the same NULL-handling problem would apply, but uniqueness was never specified and enforcing it
  unilaterally could reject legitimate data. Flagged for a decision instead.

### Open items after this step

- `Quotation` and `QuotationLine` are still not in the DbContext.
- The migration remains stale and now diverges further — the database has no `Companies`,
  `CompanySettings` or `Customers` tables, and `Products` still has its pre-rework columns.
- Whether `Product.Barcode` should be unique per company is undecided.

---

## 2026-07-18 — Infrastructure: Quotation, QuotationLine and the initial migration

**Tool:** Claude Code (Opus 4.8)

### Prompt

> Add Quotation and QuotationLine to InventoryErpDbContext — same pattern as before, Fluent API
> config in OnModelCreating. This one needs relationships: Quotation has many QuotationLine (cascade
> delete on the parent), QuotationLine references Product, Quotation references Customer. Configure
> these via Fluent API (still no navigation properties on the Domain entities unless you think
> they're necessary now that relationships are involved — flag it if you think they are).
>
> Now that all six entities are in the DbContext, generate the initial EF Core migration. Give me the
> exact `dotnet ef migrations add InitialCreate` command (referencing Infrastructure as the migrations
> project, Web as startup project) and the `dotnet ef database update` command to apply it.
>
> Then:
> - Update database/setup-notes.md with the final DB choice, the migration command, and steps to
>   verify persistence after a restart
> - Update data-model.md with sections for Quotation and QuotationLine, plus a short note on the
>   relationships configured
> - Update ai-prompts/implementation.md with this prompt, what Claude Code did, and your
>   accept/change/reject

### What Claude Code did

- Added `Quotations` and `QuotationLines` DbSets and their configuration classes.
- Configured the three relationships using the reference-less Fluent overloads
  (`HasOne<T>().WithMany()`), so no navigation properties were added to the domain entities:
  cascade from `QuotationLine` to `Quotation`, restrict from `QuotationLine` to `Product`, restrict
  from `Quotation` to `Customer`.
- Hit a blocker: a migration named `InitialCreate` already existed and was applied, so a second could
  not take the name and `database update` would have failed against existing tables. It stopped and
  asked how to clear it rather than deleting a migration and dropping a database unprompted.
- After approval: deleted the stale migration and snapshot, dropped the database, generated a fresh
  `20260718074523_InitialCreate` covering all six domain tables plus the eight Identity tables, and
  applied it.
- Verified the schema behaves as configured, rather than only that the commands exited zero:
  - 14 tables created.
  - Cascade: inserted a quotation with a line, deleted the quotation, line count went 1 → 0.
  - Restrict: deleting a customer holding a quotation was blocked by the foreign key.
  - The `Code IS NOT NULL` filter: three customers with a null code coexisted in one company.
  - Test rows were then deleted, leaving the database empty.
- Updated `database/setup-notes.md` (migration commands, reset procedure, a six-step
  persistence-after-restart check) and `data-model.md`.

### Accepted

- **Restrict rather than cascade on both `Customer` and `Product`.** EF's default for a required
  relationship is cascade, which would mean deleting a customer silently destroys every quotation
  ever issued to them. Restrict is the correct default for commercial records.
- **Stopping to ask before dropping the database.** Destructive and not explicitly requested, even
  though it was the only way to honour "generate the initial migration".
- **Verifying behaviour with real SQL** instead of trusting that the migration generated correctly.

### Changed from the original request

- **"Generate the initial EF Core migration"** required deleting the existing stale `InitialCreate`
  and dropping the database first. The prompt did not mention this, and it was surfaced as a
  decision rather than assumed.
- **No navigation properties were added**, following the stated default — but see the flag below,
  which recommends adding one.

### Rejected

- **Did not add `Quotation.Lines`** despite thinking it is now justified, because the prompt set
  "no navigation properties" as the default and adding one changes the domain model. Flagged for a
  decision instead of taken unilaterally.

### Flagged for decision

- **`Quotation` and `QuotationLine` form an aggregate**, and this is the first relationship where the
  absence of a navigation property has a real cost. Loading a quotation with its lines needs a
  second explicit query, and saving a quotation and its lines as one unit needs manual coordination.
  A single `ICollection<QuotationLine> Lines` on `Quotation` would be plain C# with no EF
  dependency, so it would not violate the zero-dependency rule.
- **Cascade delete does not fire on soft delete.** `Repository<T>.Remove` issues an `UPDATE`, never
  a `DELETE`, so soft-deleting a quotation leaves its lines visible and orphaned. Not yet a live bug
  — nothing deletes quotations — but any delete feature must cascade the soft delete in application
  code.

### Open items after this step

- No seed data: the database is empty, so there is no company to own any tenant-scoped record.
- Tenant isolation still enforced only in service logic.
- `Quotation` has no status/lifecycle field.
- No application-layer services, DTOs or UI for `Company`, `Customer`, `Quotation`.
