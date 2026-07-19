# Test Results

Last run: **2026-07-18**, against SQL Server 2022 local default instance.

## `dotnet test` output

```
Determining projects to restore...
All projects are up-to-date for restore.
  InventoryErp.Shared -> src\InventoryErp.Shared\bin\Debug\net10.0\InventoryErp.Shared.dll
  InventoryErp.Domain -> src\InventoryErp.Domain\bin\Debug\net10.0\InventoryErp.Domain.dll
  InventoryErp.Application -> src\InventoryErp.Application\bin\Debug\net10.0\InventoryErp.Application.dll
  InventoryErp.Infrastructure -> src\InventoryErp.Infrastructure\bin\Debug\net10.0\InventoryErp.Infrastructure.dll
  InventoryErp.Application.Tests -> tests\InventoryErp.Application.Tests\bin\Debug\net10.0\InventoryErp.Application.Tests.dll
Test run for tests\InventoryErp.Application.Tests\bin\Debug\net10.0\InventoryErp.Application.Tests.dll (.NETCoreApp,Version=v10.0)
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:   173, Skipped:     0, Total:   173, Duration: 7 s
```

## Mandatory acceptance tests

Both live in `tests/InventoryErp.Application.Tests/Acceptance/`, deliberately separate from the
per-service suites so they are easy to find and run alone
(`dotnet test --filter "FullyQualifiedName~Acceptance"`).

```
Passed QuotationCalculationTests.Quotation_with_two_lines_produces_the_hand_calculated_totals
Passed QuotationCalculationTests.Each_line_produces_its_own_hand_calculated_amounts
Passed QuotationCalculationTests.Totals_are_persisted_not_only_returned
Passed QuotationCalculationTests.Gst_is_charged_on_the_discounted_amount_not_the_gross
Passed QuotationCalculationTests.A_line_with_a_non_positive_quantity_is_rejected(quantity: 0)
Passed QuotationCalculationTests.A_line_with_a_non_positive_quantity_is_rejected(quantity: -1)
Passed QuotationCalculationTests.A_line_with_a_non_positive_quantity_is_rejected(quantity: -100)
Passed QuotationCalculationTests.A_rejected_quotation_writes_nothing_to_the_database
Passed QuotationCalculationTests.The_rejection_message_identifies_which_line_is_wrong
Passed SettingsDefaultsTests.No_setting_rows_exist_for_the_company
Passed SettingsDefaultsTests.GetSettings_returns_the_documented_defaults_when_no_rows_exist
Passed SettingsDefaultsTests.Unset_optional_fields_come_back_empty_never_null
Passed SettingsDefaultsTests.A_stored_row_overrides_the_default
Passed SettingsDefaultsTests.Pdf_generates_with_no_settings_at_all
Passed SettingsDefaultsTests.Pdf_generates_with_only_some_settings_populated
Passed SettingsDefaultsTests.Pdf_generates_when_every_optional_setting_is_blank
Passed SettingsDefaultsTests.Pdf_generates_when_the_accent_colour_is_malformed
Passed SettingsDefaultsTests.Pdf_generates_when_the_logo_path_is_set_but_the_file_is_missing
```

### 1. Quotation calculation — what it proves

Two products with known inputs, and expected values **hand-calculated in the test file's comments**
rather than derived from the implementation:

| Line | Qty | Price | Disc | GST | Gross | Discount | Tax | Total |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Hex Bolt | 10 | 24.50 | 10% | 18% | 245.00 | 24.50 | 39.69 | 260.19 |
| Safety Helmet | 5 | 349.00 | 0% | 5% | 1745.00 | 0.00 | 87.25 | 1832.25 |
| **Header** | | | | | **1990.00** | **24.50** | **126.94** | **2092.44** |

| Test | Proves |
| --- | --- |
| `..._hand_calculated_totals` | All four header figures match arithmetic done by hand |
| `Each_line_produces_its_own...` | Per-line gross, discount, tax and total are individually right, not merely summing to the right header |
| `Totals_are_persisted_not_only_returned` | Reads back from the store — a correct DTO over a wrong entity cannot pass |
| `Gst_is_charged_on_the_discounted_amount...` | Pins the order of operations. Taxing the gross would give 44.10 instead of 39.69 |
| `..._non_positive_quantity_is_rejected` | 0, −1 and −100 all rejected as `ValidationFailed` |
| `A_rejected_quotation_writes_nothing...` | One bad line rejects the whole quotation — no partial save |
| `The_rejection_message_identifies_which_line...` | Error is prefixed `Line 2:`, so a UI can point at the offending row |

These figures independently corroborate the manual browser test: quotation `QT-2026-0001` was
created through the UI with the same inputs and stored 1990.00 / 24.50 / 126.94 / 2092.44.

### 2. Settings defaults and PDF resilience — what it proves

The fixture is a company with **nothing but its required name** — no setting rows, no optional
columns.

| Test | Proves |
| --- | --- |
| `No_setting_rows_exist_for_the_company` | Establishes the precondition, so the next test cannot pass for the wrong reason |
| `..._documented_defaults_when_no_rows_exist` | `#4F46E5`, `India`, and both document-text defaults come back exactly as documented in `api-contract.md` |
| `Unset_optional_fields_come_back_empty_never_null` | Every property is non-null by contract; callers never null-check |
| `A_stored_row_overrides_the_default` | Precedence works, and untouched keys still default |
| `Pdf_generates_with_no_settings_at_all` | The PDF reads a fully-defaulted context |
| `Pdf_generates_with_only_some_settings_populated` | **Partial settings do not crash it** — a realistic half-configured company |
| `..._when_every_optional_setting_is_blank` | Blank stored values are a different path from absent rows, and also survive |
| `..._when_the_accent_colour_is_malformed` | A bad colour cannot reach the renderer and throw mid-document |
| `..._when_the_logo_path_is_set_but_the_file_is_missing` | The restored-backup case degrades to no logo |

## Summary

| Check | Result |
| --- | --- |
| `dotnet build InventoryErp.slnx` | Succeeded — **0 warnings, 0 errors** (warnings-as-errors enabled) |
| `dotnet test InventoryErp.slnx` | **173 passed, 0 failed, 0 skipped** |
| Mandatory acceptance tests | **18 passed** |
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

### `WriteTenantIsolationTests` — 12 passed

Added at final review, alongside the existing read-path isolation tests, after `CompanyId` turned
out to be client-suppliable (`debugging-notes.md` #10). Together these are what closed the
"tenant isolation is untested" gap this document previously listed.

| Test group | Verifies |
| --- | --- |
| Create ownership | A created record is owned by the *signed-in* user's company, never a posted value |
| Cross-tenant read/update/delete by id | Another company's id returns `NotFound` rather than the record |
| Ownership immutability | `UpdateAsync` never reassigns `CompanyId`, so a record cannot be moved between tenants |
| Per-tenant SKU | The same SKU may exist in two companies |
| Quotation references | A quotation cannot reference another company's customer or product |

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

### UI verification — 2026-07-18

Run after the design-system work, at 375px, 768px and 1280px.

| Check | Result |
| --- | --- |
| Static assets signed out | **200** with correct content types (was 302 — see `debugging-notes.md` #8) |
| Console errors | None |
| Network 404s | None |
| Horizontal page scroll at 375px | None — `window.scrollX` max is 0; wide tables scroll inside `.table-responsive` |
| Dark mode | Toggles, persists across navigation, no light flash on first paint |
| Mobile drawer | Opens with backdrop, closes on Escape, body scroll restored |
| Sidebar active state | Correct on Dashboard and Products |
| Dashboard figures | 8 products, 7 active, 3 below reorder — matches seeded data |
| Table filter | "helmet" → 1 of 8 items, count announced |
| Create product | Persisted, success flash, list count 8 → 9 |
| Duplicate SKU | Renders as a form error, not an exception |
| Delete modal | Names the product, deletes, flashes, count back to 8 |
| Logout | Returns to `/Account/Login` |

Test data created during this run was deleted afterwards; the database is back to its seeded state.

## Not yet covered

- No automated test exercises the database constraints — the duplicate-SKU test passes on the
  service guard, not on the unique index. The same limitation applies to the `CompanyId` foreign
  keys added in `AddCompanyForeignKeys`: the in-memory provider ignores them entirely, so they were
  verified by hand against SQL Server (`debugging-notes.md` #11) rather than by the suite.
- No web-layer or controller tests.
- No tests for `Company`, `CompanySetting`, `Customer`, `Quotation`, `QuotationLine` services —
  those services do not exist yet.
- **The end-to-end run above predates the `Product` rework and the database instance switch.** It
  should be repeated before any release; the current database is empty, so it would need a fresh
  user and product.
