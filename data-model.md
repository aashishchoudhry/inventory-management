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
| `LogoPath` | `string?` | Path to the logo used on generated documents |

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
| `Code` | `string?` | Human-readable identifier, intended to be unique within the company |
| `Mobile` | `string?` | |
| `City` | `string?` | |
| `State` | `string?` | |
| `Address` | `string?` | |

## Quotation

A priced offer issued to a customer, scoped to a company.

| Field | Type | Notes |
| --- | --- | --- |
| `Id` | `Guid` | From `BaseEntity` |
| `CompanyId` | `Guid` | Owning tenant |
| `QuotationNumber` | `string` | Required. Human-readable reference, intended to be unique per company |
| `CustomerId` | `Guid` | The customer being quoted |
| `QuotationDate` | `DateTime` | Date of issue |
| `ValidUntil` | `DateTime?` | Date the offer lapses. Null means it does not expire |
| `SubTotal` | `decimal` | Sum of line totals before tax and discount |
| `TaxAmount` | `decimal` | |
| `DiscountAmount` | `decimal` | |
| `TotalAmount` | `decimal` | Final payable amount |
| `Notes` | `string?` | |

## QuotationLine

A single product line on a quotation.

| Field | Type | Notes |
| --- | --- | --- |
| `Id` | `Guid` | From `BaseEntity` |
| `QuotationId` | `Guid` | Owning quotation |
| `ProductId` | `Guid` | Product being quoted |
| `Quantity` | `int` | Whole units, consistent with `Product.CurrentStock` |
| `UnitPrice` | `decimal` | Copied from the product's selling price at quoting time |
| `DiscountPercent` | `decimal` | Line-level discount, e.g. `5.0` for 5% |
| `GstPercent` | `decimal` | Copied from the product at quoting time |
| `TaxAmount` | `decimal` | |
| `TotalAmount` | `decimal` | Line total after discount and tax |

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

`Product`, `Customer` and `CompanySetting` all carry a `CompanyId`. Tenant isolation is currently
enforced **only in application service logic** (for example, the product SKU duplicate check is
scoped by `CompanyId`) — there is no global tenant query filter and no ambient tenant context yet.

Until an `ICurrentTenant` abstraction exists, read methods such as `GetAllAsync` return rows across
all tenants. This is a known gap, not a design decision. See the open items in
`implementation-plan.md`.

---

## Scope status

Domain entities are **complete for Core scope**: `Company`, `CompanySetting`, `Product`, `Customer`,
`Quotation`, `QuotationLine`.

`ApplicationUser` and `ApplicationRole` are deliberately **not** domain entities. They derive from
ASP.NET Identity's `IdentityUser<Guid>` / `IdentityRole<Guid>` and live in
`InventoryErp.Infrastructure/Identity`, because putting them in Domain would drag an ASP.NET
dependency into a layer that must have none.

No navigation properties exist on any entity yet — relationships between `Quotation` and
`QuotationLine`, `Product` and `Customer` are expressed as bare foreign-key `Guid`s. Navigation
properties and relationship configuration are deferred to the Infrastructure / EF Core step.
