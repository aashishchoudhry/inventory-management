# Data Model

## Conventions

All persisted entities derive from `BaseEntity`, which supplies:

| Field | Type | Notes |
| --- | --- | --- |
| `Id` | `Guid` | Primary key, defaulted to a new Guid on construction |
| `CreatedAtUtc` | `DateTime` | Stamped on insert by `ApplicationDbContext` |
| `CreatedBy` | `string?` | Username of the caller, via `ICurrentUser` |
| `ModifiedAtUtc` | `DateTime?` | Stamped on update |
| `ModifiedBy` | `string?` | Username of the caller |
| `IsDeleted` | `bool` | Soft-delete marker, hidden by a global EF query filter |

Domain entities carry **no EF Core attributes or data annotations** — `InventoryErp.Domain` has zero
dependencies. All persistence concerns (column types, lengths, indexes, relationships) are declared
in `IEntityTypeConfiguration<T>` classes under `InventoryErp.Infrastructure/Persistence/Configurations`.

---

## Company

The organisation the system is run for. Source of truth for document branding and statutory
identifiers on invoices and generated PDFs.

| Field | Type | Notes |
| --- | --- | --- |
| `Id` | `Guid` | From `BaseEntity` |
| `Name` | `string` | Required |
| `Tagline` | `string?` | |
| `Address` | `string?` | |
| `City` | `string?` | |
| `State` | `string?` | |
| `Country` | `string?` | |
| `PinCode` | `string?` | |
| `GstNumber` | `string?` | Goods and Services Tax identification number |
| `PanNumber` | `string?` | Permanent Account Number |
| `Mobile` | `string?` | |
| `Email` | `string?` | |
| `Website` | `string?` | |
| `LogoPath` | `string?` | **Root-relative web path** to the logo file, e.g. `/uploads/logos/{guid}.png` |

### `LogoPath` is a path, not binary data

The column stores a **relative file path**; the image itself lives on disk under
`wwwroot/uploads/logos/`. No image bytes are ever stored in the database.

Why a path rather than a `varbinary` column:

- The web server serves the file directly as a static asset, with normal caching and range
  support. A database blob would have to be read, buffered and streamed through the application
  on every request.
- Backups and query plans stay small. Images are typically far larger than every other row in
  the table combined.
- Moving to blob storage or a CDN later changes the stored string and the `ILogoStorage`
  implementation, nothing else.

The trade-off is that **the database and the filesystem can disagree**. A restored database
backup without the matching `wwwroot/uploads` directory leaves `LogoPath` pointing at a file that
does not exist. Backups must include both to be complete.

#### How consumers handle a dangling path

A missing file is treated as "no logo" everywhere, never as an error:

| Consumer | Behaviour when the file is gone |
| --- | --- |
| `ILogoStorage.TryReadAsync` | Returns `null` and logs a warning. Also returns null for a blank path, or one pointing outside the logos directory |
| **`QuotationPdfService`** | **Renders the header without the logo.** The PDF still generates, still returns `200`, and is simply smaller. It additionally catches any exception from logo loading, so even a storage implementation that throws cannot fail the document |
| Settings page | Renders a broken image placeholder — visible enough that someone notices and re-uploads |

This is deliberate: a quotation is a commercial document, and being unable to send one because a
decorative image moved would be far worse than sending it without the image. Verified by deleting
the file from disk and regenerating — the PDF went from one embedded image to zero, stayed valid,
and logged a warning at both layers.

Filenames are **generated GUIDs**, never the uploaded name: a client-supplied filename could
contain path-traversal segments or collide with an existing file.

#### Size in documents

No dimension constraint is enforced at upload — only a 2 MB file-size cap. The PDF header instead
scales the image to fit a **180 × 60 pt** box (about 63 × 21 mm), preserving aspect ratio and never
enlarging beyond natural size. Constraining at render time rather than upload means the original is
kept intact and a different document layout can choose its own box.

## CompanySetting

A key/value settings store scoped to a company — the source of truth for PDF branding and other
runtime-configurable behaviour later on.

| Field | Type | Notes |
| --- | --- | --- |
| `Id` | `Guid` | From `BaseEntity` |
| `CompanyId` | `Guid` | Owning company |
| `Key` | `string` | Required. Well-known keys are defined in `SettingKeys` (Shared) |
| `Value` | `string?` | Stored as text; callers parse to the type they need |

Settings are deliberately untyped strings so new settings can be added without a schema change.
The trade-off is that type safety lives in the calling code rather than the database.

---

## Product

A sellable item, scoped to a company.

| Field | Type | Notes |
| --- | --- | --- |
| `Id` | `Guid` | From `BaseEntity` |
| `CompanyId` | `Guid` | Owning tenant |
| `Name` | `string` | Required, max 200 |
| `Sku` | `string` | Required, max 50. Unique per company, filtered on `IsDeleted = 0` |
| `Barcode` | `string?` | EAN/UPC, max 50. Not every item carries one |
| `Description` | `string?` | Max 1000 |
| `SellingPrice` | `decimal` | `decimal(18,2)` |
| `GstPercent` | `decimal` | `decimal(5,2)`. Rate as a percentage, e.g. `18.0` for 18% |
| `CurrentStock` | `int` | |
| `ReorderLevel` | `int` | |
| `Status` | `ProductStatus` | Persisted as `int` (`Active`, `Discontinued`) |
| `IsBelowReorderLevel` | `bool` | Computed (`CurrentStock <= ReorderLevel`), not mapped |

Indexes: unique on `(CompanyId, Sku)` filtered to non-deleted rows, plus a non-unique index on
`CompanyId` for tenant-scoped queries.

## Customer

A party the company sells to, scoped to a company.

| Field | Type | Notes |
| --- | --- | --- |
| `Id` | `Guid` | From `BaseEntity` |
| `CompanyId` | `Guid` | Owning tenant |
| `Name` | `string` | Required |
| `Code` | `string?` | Human-readable identifier, max 50. Unique within the company **when present** |
| `Mobile` | `string?` | Max 20 |
| `City` | `string?` | Max 100 |
| `State` | `string?` | Max 100 |
| `Address` | `string?` | Max 500 |

### Optional-but-unique: `Customer.Code`

`Code` is both optional and unique per company, which is a combination SQL Server does not handle
the obvious way. **SQL Server treats two NULLs as equal for uniqueness purposes**, so a plain unique
index on `(CompanyId, Code)` would permit only one customer without a code per company — the second
code-less customer would fail with a constraint violation.

The index is therefore filtered on `Code IS NOT NULL` as well as the usual soft-delete clause:

```
WHERE [IsDeleted] = 0 AND [Code] IS NOT NULL
```

This enforces uniqueness across customers that have a code, while allowing any number without one.
This is not visible from the entity alone — `public string? Code` looks like an ordinary optional
field. The same pattern will be needed for any future optional-but-unique field; `Product.Barcode`
is the obvious candidate if barcodes are ever required to be unique.

## Quotation

A priced offer issued to a customer, scoped to a company.

| Field | Type | Notes |
| --- | --- | --- |
| `Id` | `Guid` | From `BaseEntity` |
| `CompanyId` | `Guid` | Owning tenant |
| `QuotationNumber` | `string` | Required, max 50. Unique per company, filtered on `IsDeleted = 0` |
| `CustomerId` | `Guid` | The customer being quoted |
| `QuotationDate` | `DateTime` | Date of issue |
| `ValidUntil` | `DateTime?` | Date the offer lapses. Null means it does not expire |
| `SubTotal` | `decimal` | Sum of line totals before tax and discount |
| `TaxAmount` | `decimal` | |
| `DiscountAmount` | `decimal` | |
| `TotalAmount` | `decimal` | Final payable amount |
| `Notes` | `string?` | Max 2000 |

All four money fields are `decimal(18,2)`. Indexed on `CompanyId` and `CustomerId`.

## QuotationLine

A single product line on a quotation.

| Field | Type | Notes |
| --- | --- | --- |
| `Id` | `Guid` | From `BaseEntity` |
| `QuotationId` | `Guid` | Owning quotation |
| `ProductId` | `Guid` | Product being quoted |
| `Quantity` | `int` | Whole units, consistent with `Product.CurrentStock` |
| `UnitPrice` | `decimal` | Copied from the product's selling price at quoting time |
| `DiscountPercent` | `decimal` | `decimal(5,2)`. Line-level discount, e.g. `5.0` for 5% |
| `GstPercent` | `decimal` | `decimal(5,2)`. Copied from the product at quoting time |
| `TaxAmount` | `decimal` | `decimal(18,2)` |
| `TotalAmount` | `decimal` | `decimal(18,2)`. Line total after discount and tax |

Money is `decimal(18,2)`; percentages are `decimal(5,2)`. Indexed on `QuotationId` and `ProductId`.

### Relationships

Configured entirely via Fluent API in the entity configuration classes. The domain entities still
hold **bare foreign-key `Guid`s and no navigation properties**, so the relationships are declared
with the reference-less overloads (`HasOne<T>().WithMany()`).

| From | To | Delete behaviour | Why |
| --- | --- | --- | --- |
| `QuotationLine` | `Quotation` | **Cascade** | A line has no life of its own; deleting the quotation removes its lines |
| `QuotationLine` | `Product` | **Restrict** | A quoted product cannot be hard-deleted out from under the line |
| `Quotation` | `Customer` | **Restrict** | Deleting a customer must never silently destroy the record of what was quoted to them |

Verified against the created schema: deleting a parent quotation removed its line (1 → 0), and
deleting a customer holding a quotation was blocked by the foreign key.

#### Cascade delete does not fire on soft delete

This is the important caveat. Cascade is a **database-level** `ON DELETE CASCADE`, and the
application soft-deletes — `Repository<T>.Remove` sets `IsDeleted = true` and issues an `UPDATE`,
never a `DELETE`. So soft-deleting a quotation leaves its lines with `IsDeleted = false`: the parent
disappears from queries while the children remain visible and orphaned.

Nothing in the codebase currently soft-deletes a quotation, so this is not yet a live bug, but any
quotation delete feature must explicitly cascade the soft delete to its lines in the application
layer. The database cascade only protects against a genuine hard delete.

### Why totals are stored, not computed

`Quotation` and `QuotationLine` persist their monetary figures rather than deriving them on read,
and each line copies `UnitPrice` and `GstPercent` from the product at the moment of quoting. A
quotation is a commercial document: it must continue to show the numbers it was issued with even
after product prices or tax rates change. The trade-off is that stored totals can drift from their
inputs, so recalculation belongs in one place in the application layer.

`QuotationLine` has no `CompanyId`. Its tenant is derived from its parent quotation, so duplicating
it here would create two sources of truth that could disagree.

---

## Multi-tenancy

`Product`, `Customer`, `Quotation` and `CompanySetting` all carry a `CompanyId`. Isolation is
enforced **in application service logic**. The database enforces *referential* integrity on that
column, but not tenant scoping — see the table below for which layer does what.

### Current state — accurate as of the final review

| Path | Status |
| --- | --- |
| **Reads — list and search** | **Isolated and tested.** `GetAllAsync` and `SearchAsync` on products, customers and quotations take `companyId` and filter on it. Tests assert a second company's records never appear |
| **Reads — by id** | **Isolated and tested.** `GetByIdAsync` takes `companyId` and returns `NotFound` for another tenant's record, so an id cannot be probed. **This was not true until the final review** — the method took no company argument at all |
| **Writes — create** | **Isolated and tested.** The tenant is an explicit service argument resolved from the signed-in user. `CompanyId` was removed from the request DTOs entirely, so it cannot be model-bound from a posted form |
| **Writes — update and delete** | **Isolated and tested.** Both take `companyId`; update never reassigns it, so a record cannot be moved between tenants |
| **Database — referential integrity** | **Enforced.** All four tables have a `Restrict` foreign key from `CompanyId` to `Companies.Id` (migration `AddCompanyForeignKeys`). A forged or orphan tenant id is rejected by SQL Server with error 547, and a company holding records cannot be deleted |
| **Database — tenant scoping** | **Absent.** There is still no global query filter on `CompanyId`, so the database will not stop one *real* company's id being used to reach another's rows. That remains the application's job |

The tenant comes from `ICurrentCompanyProvider`, which currently resolves the single seeded
company because nothing links a user to one.

### The defect that was fixed

Until final review, `CompanyId` was a **bindable property on the product request DTOs** and rendered
as a form field. A signed-in user could create a product owned by another company, and — via the
hidden field on the edit form — move an existing product into one. `GetByIdAsync` and `DeleteAsync`
performed no company check at all.

The fix removed the property from the DTOs and made the tenant an explicit argument on every
affected method. Full account in `ai-prompts/debugging.md` #10; proof in
`tests/…/Acceptance/WriteTenantIsolationTests.cs`.

### What is still missing

Scoping depends on **every service remembering to filter**, and on callers passing the right
company. A global EF query filter on `CompanyId` — mirroring the existing soft-delete filter —
would make the whole class of bug structurally impossible, and is the right fix before a second
company exists. A company claim on `ApplicationUser` is the other half.

---

## Scope status

Domain entities are **complete for Core scope**: `Company`, `CompanySetting`, `Product`, `Customer`,
`Quotation`, `QuotationLine`.

`ApplicationUser` and `ApplicationRole` are deliberately **not** domain entities. They derive from
ASP.NET Identity's `IdentityUser<Guid>` / `IdentityRole<Guid>` and live in
`InventoryErp.Infrastructure/Identity`, because putting them in Domain would drag an ASP.NET
dependency into a layer that must have none.

No navigation properties exist on any entity — relationships are expressed as bare foreign-key
`Guid`s and configured in Infrastructure with EF's reference-less `HasOne<T>().WithMany()` overload.

This is worth knowing because it caused a real gap: with no navigation property, EF infers no
relationship, so the `CompanyId` columns had an index but **no foreign key** until the
`AddCompanyForeignKeys` migration added them explicitly. Any future tenant-scoped entity must
configure its `Company` relationship by hand — nothing will infer it.
