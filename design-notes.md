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
