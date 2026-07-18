# Design Prompts

Prompts covering the domain model design. Persistence and DbContext work is in
[implementation.md](implementation.md).

---

## 2026-07-18 — Domain entities, in three steps

**Tool:** Claude Code (Opus 4.8)

The domain model was built in three prompts, each adding two entities. Each carried the same
standing constraints:

> Plain properties only, no EF Core attributes or data annotations — this project has zero
> dependencies. Use Guid for ids unless you think int fits better here, your call, just tell me why.

### Step 1 — `Company` and `CompanySetting`

Fields as specified, plus a note that `CompanySetting` is a key/value settings store and the source
of truth for PDF branding later.

**Accepted:** both entities deriving from `BaseEntity`, inheriting Guid ids, audit fields and soft
delete; `required` on `Name` and `Key`, which is plain C# 11 and not a data annotation, so the
zero-dependency rule holds.

**Flagged:** company branding now has two possible homes — the typed `Company.Name` column and a
`CompanySetting` row keyed `"Company.Name"`. Which wins should be settled before the PDF work, or
they will drift.

### Step 2 — `Product` and `Customer`

**Conflict found.** `Product` already existed from the scaffold's vertical slice with a different
field set. The tool stopped and presented the difference rather than overwriting:

| Prompt | Existing scaffold |
| --- | --- |
| `companyId`, `barcode`, `gstPercent` | — (new) |
| `sellingPrice` | `UnitPrice` (rename) |
| `currentStock` | `QuantityOnHand` (rename) |
| — | `Description`, `ReorderLevel`, `Status` (would be dropped) |

**Decision taken:** apply the specified fields but keep the extras, so the reorder badge and
Active/Discontinued lifecycle survived. The change was then propagated across the whole vertical
slice — DTOs, EF config, service, controller, four views and tests — in one step to keep the build
green.

**Accepted:** making SKU uniqueness **per tenant** rather than global, with a test proving two
tenants can hold the same SKU.

### Step 3 — `Quotation` and `QuotationLine`

**Accepted:** storing monetary totals rather than recomputing them, with each line copying
`UnitPrice` and `GstPercent` from the product at quoting time. A quotation is a commercial document
and must keep the figures it was issued with.

**Accepted:** `QuotationLine` having no `CompanyId` — its tenant derives from the parent quotation,
so duplicating it would create two sources of truth that could disagree.

---

## Cross-cutting design decisions

### `Guid` over `int`, consistently

Asked at every step. The answer was Guid each time, for compounding reasons:

1. `BaseEntity.Id` and `IRepository<T>.GetByIdAsync(Guid)` already fix the key type; deviating per
   entity would mean abandoning the base class or making it generic on key type.
2. In a multi-tenant system, non-guessable ids mean a leaked identifier from `/Products/Details/{id}`
   does not enumerate another tenant's catalogue the way sequential ints would.
3. For `Quotation` / `QuotationLine` it is load-bearing: a Guid parent id exists in memory before the
   parent is saved, so a quotation and its lines can be built as one object graph and saved once.
   Identity ints would force insert-then-read-back-then-insert.

### No navigation properties

Requested, and followed throughout — relationships are bare foreign-key `Guid`s, configured with
EF's reference-less Fluent overloads.

**Flagged at step 3:** `Quotation` / `QuotationLine` is a genuine aggregate, and this is the first
relationship where the absence has a real cost — loading a quotation with its lines needs a second
query, and saving both as a unit needs manual coordination. An `ICollection<QuotationLine> Lines`
would be plain C# with no EF dependency. Not added, because "no navigation properties" was the stated
default and changing the domain model unilaterally was not appropriate. Still open.

### Decisions raised but left to the user

- **`Quantity` is `int`, not `decimal`.** Consistent with `Product.CurrentStock`, but this blocks
  selling anything by weight or length. Changing it later means migrating a table with data.
- **No `QuotationStatus`** (draft / sent / accepted / expired). Not in the specified fields, but most
  quotation workflows need one, and adding it before quotations exist in the database is cheaper.
- **`Product.Barcode` uniqueness** undecided. A scanner resolving one barcode to two products is a
  real problem, but enforcing uniqueness could reject legitimate data such as variants sharing a
  manufacturer barcode.
