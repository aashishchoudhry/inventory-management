# Test Results

Last run: **2026-07-18**, against SQL Server 2022 local default instance.

## Summary

| Check | Result |
| --- | --- |
| `dotnet build InventoryErp.slnx` | Succeeded — **0 warnings, 0 errors** (warnings-as-errors enabled) |
| `dotnet test InventoryErp.slnx` | **14 passed, 0 failed, 0 skipped** |
| `dotnet ef dbcontext info` | Model loads under the SQL Server provider |
| Schema | 14 tables created (6 domain + 8 ASP.NET Identity) |

## Automated tests

### `ServiceResultTests` — 6 passed

| Test | Verifies |
| --- | --- |
| `Success_carries_the_payload` | Success sets payload, no error, empty validation errors |
| `NotFound_is_a_failure_with_no_payload` | `NotFound` status, null data, message preserved |
| `Invalid_collects_the_validation_errors` | `ValidationFailed` status, all errors retained |
| `Conflict_reports_the_conflict_status` | `Conflict` status and message |
| `Implicit_conversion_from_a_value_produces_success` | `ServiceResult<int> r = 42` yields success |
| `Non_generic_success_has_no_error` | Non-generic `Success()` has no error |

### `ProductServiceTests` — 8 passed

| Test | Verifies |
| --- | --- |
| `CreateAsync_persists_the_product_and_returns_it` | Row written; SKU, price, GST, `CompanyId` returned |
| `CreateAsync_allows_the_same_sku_in_a_different_company` | SKU uniqueness is per tenant, not global |
| `CreateAsync_returns_Conflict_for_a_duplicate_sku` | Duplicate within a company rejected; no second row |
| `CreateAsync_returns_Invalid_for_a_blank_sku` | Whitespace SKU → `ValidationFailed` with messages |
| `GetByIdAsync_returns_NotFound_for_an_unknown_id` | Unknown id → `NotFound`, null data |
| `UpdateAsync_changes_the_stored_values` | Fields updated; computed `IsBelowReorderLevel` recalculated |
| `DeleteAsync_soft_deletes_and_hides_the_product` | Row hidden from queries but present under `IgnoreQueryFilters()` |
| `DeleteAsync_returns_NotFound_for_an_unknown_id` | Unknown id → `NotFound` |

## Manual verification

### Schema behaviour — direct SQL against SQL Server

These cover behaviour the in-memory provider cannot exercise. Test rows were inserted, assertions
made, then deleted, leaving the database empty.

| Check | Method | Result |
| --- | --- | --- |
| Cascade delete | Inserted a quotation with one line, deleted the quotation | Line count 1 → 0 — **pass** |
| Restrict delete (`Customer`) | Deleted a customer holding a quotation | Blocked by FK — **pass** |
| Nullable unique index | Inserted three customers with null `Code` in one company | All three accepted — **pass** |
| Filtered index present | INSERT without `QUOTED_IDENTIFIER ON` | Failed with SET-options error, confirming filtered indexes exist — **pass** |

### End-to-end through the application

Run during the scaffold step against the running app.

| Step | Result |
| --- | --- |
| Register user, log in | Succeeded; Identity wired correctly |
| `/Products` unauthenticated | Redirected to login — `[Authorize]` enforced |
| Create a product | Persisted and listed |
| Low stock (3 ≤ 5) | "Reorder" badge rendered |
| Duplicate SKU | Rendered *"A product with SKU 'SKU-1001' already exists."* as a form message, **not** an exception — confirms the `ServiceResult` pattern end-to-end |
| Audit stamping | `CreatedBy` persisted as the signed-in username, confirmed by direct SQL |
| Server error log | Clean |

## Not yet covered

- No automated test exercises the database constraints — the duplicate-SKU test passes on the
  service guard, not on the unique index.
- No web-layer or controller tests.
- No tests for `Company`, `CompanySetting`, `Customer`, `Quotation`, `QuotationLine` services —
  those services do not exist yet.
- Tenant isolation is untested, and currently unenforced.
- **The end-to-end run above predates the `Product` rework and the database instance switch.** It
  should be repeated before any release; the current database is empty, so it would need a fresh
  user and product.
