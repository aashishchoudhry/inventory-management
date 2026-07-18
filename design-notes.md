# Design Notes

## Layering

Clean Architecture with the dependency rule enforced by project references, not convention.
`InventoryErp.Domain.csproj` has no `ProjectReference` and no `PackageReference` — the constraint is
mechanical, so a violation fails the build rather than passing review.

### The one deliberate concession

`InventoryErp.Web` references `InventoryErp.Infrastructure`. Strictly, the presentation layer should
not know about persistence. It does here because the web project is also the **composition root**:
it must call `AddInfrastructure()` and register Identity's EF stores, which requires naming concrete
types. Controllers depend only on Application interfaces.

If this needs enforcing mechanically rather than by discipline, an ArchUnitNET test asserting that
no type under `Controllers` references `Infrastructure` is the usual approach.

### Where Identity lives

`ApplicationUser` and `ApplicationRole` derive from ASP.NET Identity types, so they sit in
Infrastructure, not Domain. Putting them in Domain would drag an ASP.NET dependency into the layer
that must have none. The consequence is that domain code cannot reference a user directly — audit
fields store a **username string**, captured through the `ICurrentUser` abstraction, rather than a
foreign key to `AspNetUsers`.

That is a real trade-off: usernames can change, and there is no referential integrity between an
audit field and the user table. It was accepted because the alternative compromises the dependency
rule, and audit fields are a historical record of who acted at the time.

## Backend Design

### Cookie-based Identity, no external providers, no JWT

Authentication uses **ASP.NET Core Identity with cookie authentication** — username and password
only. No external/social providers, no JWT bearer tokens. This matches Core scope.

**Why cookies rather than JWT:** the frontend is server-rendered Razor MVC, not a SPA or a mobile
client. The browser is the only consumer, and cookies are what a browser handles natively — sent
automatically, `HttpOnly` so script cannot read them, and revocable server-side by dropping the
session. JWTs solve a problem this application does not have (stateless auth across independently
deployed services) while adding real ones: tokens cannot be revoked before expiry, and storing them
in a browser means choosing between `localStorage`, which is readable by any injected script, and a
cookie — at which point the cookie was the simpler answer.

If an API for a mobile client or third-party integration is added later, bearer tokens can be layered
alongside for those endpoints without disturbing the cookie flow for the web UI.

Registration is configured with `RequireConfirmedAccount = false`, so no email confirmation step and
no mail transport dependency. That is a development convenience and should be revisited before any
real deployment. Password policy is left at Identity defaults apart from a raised minimum length of 8.

### PDF generation with QuestPDF

Quotation PDFs are rendered by **QuestPDF** in `InventoryErp.Infrastructure/Documents`. The
Application layer sees only `IQuotationPdfService`, so the rendering library never leaks upward.

**Why QuestPDF:** a fluent C# layout API with no HTML engine or Chromium process to install, no
external binaries, and precise control over a document that must look the same every time. The
HTML-to-PDF alternatives need a browser runtime — a large deployment dependency for a page of
tables.

> **Licence.** QuestPDF's Community licence is free only for organisations below a revenue
> threshold; above it a paid licence is required. This is the same class of trap as FluentAssertions
> (rejected earlier for exactly this reason), so it is called out here rather than buried: the
> licence type is declared explicitly in `AddInfrastructure`, and this decision needs revisiting if
> the business crosses that threshold.

**Rendering degrades rather than fails.** A missing or unreadable logo produces a header without a
logo, never an exception — a commercial document must still be sendable when a decorative asset has
moved. The logo is scaled to fit a 180 × 60 pt box, preserving aspect ratio. See `data-model.md`.

The older `IPdfGenerator` placeholder from the initial scaffold was **not** implemented: its
`RenderAsync(string templateName, object model)` signature is stringly-typed and untyped, where
`IQuotationPdfService` is specific and compile-time safe. That interface is now dead code.

### Settings are key/value rows, not fixed columns

Company settings live in `CompanySetting` — a `(CompanyId, Key, Value)` table — rather than as
columns on `Company`. `SettingsService` presents them as a strongly-typed `CompanySettingsDto`, so
callers get compile-time safety while storage stays schema-free.

**Why:** adding a setting is an `INSERT`, not a migration. Settings accumulate constantly in an ERP
— invoice terms, a footer line, a brand colour, later a low-stock threshold, an SKU prefix, a
document number format. Each one as a column means a migration, a deployment, and a schema change
for what is really just configuration. Key/value also lets a setting exist for one company and not
another without a nullable column for every optional feature.

**What it costs, honestly:**

- **No type safety at the storage layer.** Every value is a string; parsing lives in the service.
  `CompanySettingsDto` restores type safety at the boundary, but the database cannot enforce it.
- **No database constraints.** A column could be `CHECK`-constrained or `NOT NULL`; a key/value row
  cannot. All validation is application-side, which means a direct SQL write bypasses it.
- **No referential integrity or indexing on values.** Querying "all companies with a given setting"
  means a string comparison, not an indexed column lookup.
- **Keys are stringly-typed.** `SettingKeys` centralises them as constants, but a typo in a literal
  compiles fine and silently reads as "missing". Renaming a key orphans existing rows.

The trade is deliberate: settings are read rarely, written rarely, and never joined on. Nothing
here is worth a migration each time.

### The overlap with `Company` columns

`Address`, `City`, `State`, `Country`, `PinCode`, `GstNumber` and `PanNumber` exist **both** as
typed columns on `Company` and as settings keys. This is a genuine duplication, flagged when
`Company` was first added.

The resolution is a documented precedence rather than a merge: **the setting wins; the column is
the fallback.** An unconfigured company shows its real registered details on first visit, and once
someone saves the settings form, the settings table is authoritative.

The unresolved part: saving settings does **not** write back to the `Company` columns, so after the
first save the two can disagree. Nothing currently reads those columns for documents, so it is not
yet a live bug — but it must be settled before anything else consumes `Company.Address`. The two
sane endings are to drop the duplicated columns, or to have `SettingsService` write both.

### Identity user keyed on `Guid`

`ApplicationUser` derives from `IdentityUser<Guid>` rather than the default `IdentityUser`, which is
keyed on `string`. This makes user ids consistent with every domain entity, all of which use `Guid`
primary keys through `BaseEntity`, and avoids `nvarchar(450)` key columns and their index cost.

The custom fields are `FullName` and `IsActive`. `IsActive` allows an account to be disabled without
deleting it, preserving audit history.

### Identity lives in Infrastructure, registered in Web

The types live in `InventoryErp.Infrastructure/Identity` because they derive from ASP.NET Identity
base classes, which must not reach Domain. Registration happens in `Program.cs` rather than
`AddInfrastructure()`, because `AddDefaultUI()` ships in the ASP.NET-only
`Microsoft.AspNetCore.Identity.UI` package and calling it from Infrastructure would force a
framework reference into that layer.

### One DbContext for domain and Identity

`InventoryErpDbContext` inherits `IdentityDbContext<ApplicationUser, ApplicationRole, Guid>`, so
Identity tables and domain tables share a single context and a single migration history. A separate
Identity context was considered and rejected: two contexts over one database means two migration
histories and contested ownership of shared concerns, for no benefit at this scale.

The trade-off is that Identity and domain concerns are coupled in one class. If Identity were ever
moved to a separate store, this would need unpicking.

## `ServiceResult<T>` instead of exceptions

Application services return `ServiceResult<T>` carrying a `ResultStatus` — `Success`, `NotFound`,
`ValidationFailed`, `Conflict`, `Unauthorized`, `Error`. Exceptions are reserved for genuinely
unexpected faults.

**Why:** "SKU already exists" and "no product with that id" are ordinary, expected outcomes of a
valid request, not exceptional conditions. Modelling them as exceptions means using stack unwinding
for control flow, paying the cost of exception construction on a predictable path, and forcing every
caller into `try`/`catch` to handle a routine case. It also loses information: a caller has to
inspect exception types to distinguish "not found" from "conflict".

Making the outcome part of the return type means the compiler surfaces it, and the web layer maps
status onto HTTP in one place — `ProductsController.Failed()` translates `NotFound` to `404`,
`Unauthorized` to `403`, and anything else to a problem response.

**Trade-off:** callers can ignore a returned result in a way they cannot ignore an exception. A
service call whose result is discarded fails silently. This is the standard criticism of result
types and is worth watching for in review.

`ServiceResult<T>` also defines an implicit conversion from `T`, so a service can `return dto;`
directly on the success path.

## Validation in two layers, deliberately

| Layer | Mechanism | Purpose |
| --- | --- | --- |
| DTOs (`CreateProductRequest`) | Data annotations | Client-side and model-state validation |
| Database | Fluent API max lengths, indexes | Last line of defence |
| Domain entities | **Neither** | Kept dependency-free |

These are separate concerns and may legitimately differ, provided DTO limits are at or below column
limits. Business rules that are not simple field constraints — SKU uniqueness within a company —
live in the application service, because they need to query.

## Soft delete and audit as cross-cutting concerns

Both are handled centrally in `InventoryErpDbContext` and keyed off `BaseEntity`, so a new entity
inherits them with no extra wiring:

- A global query filter of `!IsDeleted` is applied by reflection to every `BaseEntity` type.
- `SaveChangesAsync` stamps created/modified fields from `ICurrentUser`.

**Known consequence:** because deletes are soft, the database's `ON DELETE CASCADE` never fires —
`Remove` issues an `UPDATE`, not a `DELETE`. Cascading a soft delete to children is an application-layer
responsibility. See `data-model.md`.

## Database Design

### Fluent API, not attributes

All EF Core mapping is declared with the **Fluent API** in `IEntityTypeConfiguration<T>` classes
under `InventoryErp.Infrastructure/Persistence/Configurations`. `InventoryErpDbContext.OnModelCreating`
picks them up with a single `ApplyConfigurationsFromAssembly` call.

**Why:** the domain layer must stay dependency-free. Data annotations like `[Required]`,
`[MaxLength]` or `[Column]` live in `System.ComponentModel.DataAnnotations` and
`Microsoft.EntityFrameworkCore`, so putting them on `Company` or `Product` would force
`InventoryErp.Domain` to reference EF Core. That would invert the dependency rule: the innermost
layer would depend on a persistence technology, and swapping ORMs — or unit-testing the domain in
isolation — would mean touching every entity. `InventoryErp.Domain.csproj` currently has no package
or project references at all, and Fluent API is what keeps it that way.

Two further benefits:

- **Expressiveness.** Composite keys, filtered indexes, value conversions, owned types and query
  filters have no attribute equivalent. The `(CompanyId, Sku)` unique index filtered on
  `IsDeleted = 0` can only be declared fluently.
- **Locality of persistence concerns.** Column types, lengths and indexes sit together in one file
  per entity, rather than being scattered through the domain model as annotations.

The cost is that mapping lives away from the entity it describes, so a field added to a domain class
will silently take EF defaults until the matching configuration is updated.

### Configuration file per entity

Each entity gets its own configuration class (`CompanyConfiguration`, `CompanySettingConfiguration`,
`ProductConfiguration`) rather than inline `builder.Entity<T>()` calls in `OnModelCreating`. This
keeps `OnModelCreating` to a handful of lines regardless of how many entities exist, and makes the
mapping for any entity easy to find.

### Cross-cutting behaviour in the DbContext

Two concerns are handled centrally in `InventoryErpDbContext` rather than per entity:

- **Soft delete** — a global query filter of `!IsDeleted` is applied by reflection to every type
  deriving from `BaseEntity`, so new entities get it automatically.
- **Audit stamping** — `SaveChangesAsync` sets the created/modified fields from `ICurrentUser`.

Both are driven off `BaseEntity`, so adding an entity requires no extra wiring for either.

### Validation lives in two places, deliberately

Fluent API max lengths protect the database. DTO data annotations
(`CreateProductRequest`, etc.) drive client-side and model-state validation in the web layer. These
are separate concerns and are allowed to differ — the DTO limits should be at or below the column
limits. The domain entities themselves carry neither.
