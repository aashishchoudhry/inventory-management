# Acceptance Criteria

> **Scope caveat — read this first.** Criteria 1–12 are the ones **stated explicitly in prompts**
> during development. They were not transcribed from the project guide, which has never been shared
> with the assistant, so **this list cannot be confirmed complete against that document.** Anyone
> signing off should check it against the guide directly.
>
> Criteria 13–16 were added at wrap-up to record behaviour that was built and verified but for
> which no criterion had been stated. They are labelled *(inferred)* rather than presented as
> requirements.
>
> `requirements-analysis.md` is likewise **reverse-engineered from the delivered system**, not
> transcribed from the guide.

## Core

**Status: 16 of 16 met.** No criterion is outstanding. Two carry caveats, noted inline.

| # | Criterion | Status | Verified |
| --- | --- | --- | --- |
| 1 | Data persists after an application restart | **Met** | 2026-07-18 |
| 2 | A user can log in with the seeded credentials | **Met** | 2026-07-18 |
| 3 | A user can log out | **Met** | 2026-07-18 |
| 4 | Protected pages are inaccessible when signed out | **Met** | 2026-07-18 |
| 5 | Invalid credentials produce a clear error, not a crash | **Met** | 2026-07-18 |
| 6 | A user can list and search products from the database | **Met** | 2026-07-18 |
| 7 | A user can list customers from the database | **Met** | 2026-07-18 |
| 8 | A user can create a quotation with multiple line items via the UI | **Met** | 2026-07-18 |
| 9 | A user can view the quotation list and open a detail view | **Met** | 2026-07-18 |
| 10 | Backend validation rejects invalid quotations | **Met** | 2026-07-18 |
| 11 | Global search returns relevant products and customers (and quotations) | **Met** | 2026-07-18 |
| 12 | Mandatory xUnit tests pass | **Met** | 2026-07-18 |
| 13 | Company settings can be stored and edited *(inferred)* | **Met** | 2026-07-18 |
| 14 | A quotation can be exported as a PDF *(inferred)* | **Met — with caveat** | 2026-07-18 |
| 15 | A dashboard shows live counts from the database *(inferred)* | **Met** | 2026-07-18 |
| 16 | Data is isolated per company on both read and write paths *(inferred)* | **Met** | 2026-07-18 |

### 1. Data persists after an application restart

Seeded on first launch, then the application was stopped and restarted.

| | Companies | Products | Customers | Users | Roles |
| --- | --- | --- | --- | --- | --- |
| First launch | 1 | 8 | 7 | 1 | 3 |
| After restart | 1 | 8 | 7 | 1 | 3 |

Counts identical — data survived, and the seeder correctly skipped rather than duplicating
(`Seed skipped: database already contains a company.` in the startup log). Data was confirmed both
by direct SQL and by loading `/Products`, which listed all 8 seeded products.

### 2. A user can log in with the seeded credentials

`admin@inventoryerp.local` / `Admin@123456` signed in successfully at `/Account/Login`. The
navigation bar then showed the username and a Log out button, and `/Products` rendered the seeded
data.

Requesting `/Products` while signed out redirected to `/Account/Login?ReturnUrl=%2FProducts`;
after a successful sign-in the browser landed back on `/Products`, so the return-URL round trip
works.

### 3. A user can log out

The nav "Log out" control is a POST form with an antiforgery token, not a link. Clicking it ended
the session and redirected to `/Account/Login`; `/Products` then redirected to login again,
confirming the cookie was cleared.

### 4. Protected pages are inaccessible when signed out

Verified by direct HTTP request while unauthenticated:

| Path | Result |
| --- | --- |
| `/` | 302 → `/Account/Login?ReturnUrl=%2F` |
| `/Home/Privacy` | 302 → `/Account/Login?ReturnUrl=%2FHome%2FPrivacy` |
| `/Products` | 302 → `/Account/Login?ReturnUrl=%2FProducts` |
| `/Account/Login` | 200 |

Enforced by a global fallback authorization policy, so protection is the default rather than
something each controller must opt into.

### 5. Invalid credentials produce a clear error

Submitting the correct email with a wrong password redisplayed the form with *"Invalid email or
password."* in an alert — no exception, no stack trace. The same message is returned for an unknown
account and an inactive account, so the form cannot be used to enumerate valid email addresses.

### 6. A user can list and search products from the database

**Listing.** `/Products` renders all 8 seeded products with name, SKU, barcode, selling price,
GST % and stock, headed "Showing 1–8 of 8". Data comes from SQL Server through
`ProductService.GetAllAsync`, scoped to the company via `ICurrentCompanyProvider`.

**Search**, verified against the real database:

| Query | Matches on | Result |
| --- | --- | --- |
| `helmet` | Name | 1 — Safety Helmet (Yellow, ISI Marked) |
| `HELMET` / `Helmet` | Name, case-insensitive | 1 — same product |
| `PPE-HLM` | SKU | 1 — Safety Helmet |
| `8901234500048` | Barcode | 1 — Safety Helmet |
| `wheel` | Name | 1 — Cut-Off Wheel 4 inch |
| `zzz-nothing` | — | 0, with a "No products match" state |
| *(blank)* | — | All 8 |

**Paging**, verified at `pageSize=3`: page 1 shows 1–3 of 8, page 2 shows 4–6, page 3 shows 7–8,
"Page N of 3" throughout, and Previous/Next carry the search term across pages.

**Empty and error states** all render distinctly — no products at all, search matched nothing, page
past the end, and invalid paging arguments.

Covered by 30 passing unit tests, including tenant isolation (a search does not return another
company's products) and exclusion of soft-deleted rows.

### 7. A user can list customers from the database

`/Customers` renders all 7 seeded customers ordered by name, headed "Showing 1–7 of 7", with name,
code, mobile, city and state. Data comes from SQL Server through `CustomerService.GetAllAsync`,
scoped to the company via `ICurrentCompanyProvider`.

| Check | Result |
| --- | --- |
| All seeded customers listed | 7 of 7, ordered by name |
| Null `Code` / `Mobile` | "Walk-in / Counter Sales" renders em-dashes, not blanks or errors |
| Paging | `pageSize=3` → "Showing 1–3 of 7", "Page 1 of 3"; page 2 → 4–6 |
| Page past the end | `page=4` → "Page 4 is empty · There are only 7 customers in this list" |
| Invalid paging | `page=0` → "Page number must be 1 or greater." |
| Sidebar | Customers link present under Sales, highlights when active |
| Console errors | None |

Covered by 11 unit tests, including ordering, tenant isolation (a company sees only its own
customers), exclusion of soft-deleted rows, and null-code handling.

### 8. A user can create a quotation with multiple line items via the UI

Created `QT-2026-0001` for Bengal Textile Mills with **two lines** through the browser:

| Line | Product | Qty | Unit price | Disc % | GST % | Tax | Total |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | Hex Bolt M10 x 50mm | 10 | 24.50 | 10 | 18 | 39.69 | 260.19 |
| 2 | Safety Helmet (Yellow) | 5 | 349.00 | 0 | 5 | 87.25 | 1,832.25 |

Persisted header, confirmed by direct SQL: subtotal **1990.00**, discount **24.50**,
GST **126.94**, total **2092.44**. The client-side preview showed the same total before submit,
and the server figure is the one stored.

Also verified: adding and removing rows renumbers fields to a contiguous `Lines[0..n]`; selecting a
product prefills its price and GST without overwriting a manually entered price.

### 9. A user can view the quotation list and open a detail view

`/Quotations` lists number, customer, date, valid-until, line count and total, newest first, headed
"Showing 1–1 of 1". The **View** action opens `/Quotations/Details/{id}`, showing both lines with
product names and SKUs, per-line tax and totals, and the summary card.

Another company's quotation returns `404` rather than a permission error, so ids cannot be probed
for existence.

### 10. Backend validation rejects invalid quotations

Each posted directly to the server, bypassing client-side checks, so it is the **backend** under
test. Every one re-rendered the form with entered values preserved:

| Attempt | Message shown |
| --- | --- |
| No customer selected | "Select a customer." |
| Zero line items | "A quotation must have at least one line." |
| Quantity of 0 | "Quantity must be greater than zero." — inline, next to that row's Qty field |
| Discount of 150% | "Discount percent must be between 0 and 100." |

**Nothing was written for any rejected attempt** — the database held exactly 1 quotation and 2 lines
throughout, confirmed by SQL.

Covered additionally by 25 service-level tests including unknown customer, unknown product, negative
price, cross-tenant references, and `ValidUntil` earlier than the quotation date.

### 11. Global search returns relevant products and customers (and quotations)

Available from the top bar on every page. Verified against the seeded database:

| Query | Matched | Result |
| --- | --- | --- |
| `hel` | Product name | Safety Helmet (Yellow, ISI Marked) |
| `8901234500048` | Product barcode | Safety Helmet |
| `patel` | Customer name | Patel Engineering Works |
| `CUST-1001` | Customer code | Patel Engineering Works |
| `QT-2026` | Quotation number | QT-2026-0001 |
| `pa` | Mixed | 2 products + 1 customer, grouped by type |
| `h` | — | **No request sent**; page shows "Enter at least 2 characters to search." |
| `zzznothing` | — | "No results for *zzznothing*" |

**All three entity types return results**, individually and combined.

Other checks: matching is case-insensitive (`helmet` = `HELMET`); results link to real detail pages
(`/Products/Details/{id}`, `/Quotations/Details/{id}`); keyboard navigation highlights and opens
rows; Escape closes the dropdown; the box remains usable at 375px with no horizontal overflow; no
console errors.

Covered by 21 service tests, including the 2-character minimum, per-field matching for each type,
case-insensitivity, and **tenant isolation** — a second company with a same-named customer and
product never appears in the first company's results.

### 12. Mandatory xUnit tests pass

```
Passed!  - Failed: 0, Passed: 173, Skipped: 0, Total: 173, Duration: 7 s
```

Both required scenarios live in `tests/InventoryErp.Application.Tests/Acceptance/` — **18 tests,
all passing**.

**Quotation calculation.** Two products with known inputs; expected values hand-calculated in the
test file rather than derived from the implementation:

| | Gross | Discount | Tax | Total |
| --- | --- | --- | --- | --- |
| Hex Bolt — 10 @ 24.50, 10% disc, 18% GST | 245.00 | 24.50 | 39.69 | 260.19 |
| Safety Helmet — 5 @ 349.00, 0% disc, 5% GST | 1745.00 | 0.00 | 87.25 | 1832.25 |
| **Header** | **1990.00** | **24.50** | **126.94** | **2092.44** |

Plus: totals verified as *persisted* (read back from the store), GST proven to be charged after
discount, and zero/negative quantities rejected with nothing written to the database.

**Settings defaults.** A company with only its required name returns every documented default
(`#4F46E5`, `India`, both document-text strings) and no nulls. The PDF generator then survives every
degraded settings shape: none at all, partially populated, all-blank values, a malformed accent
colour, and a logo path whose file is missing.

Full output and a per-test explanation in [test-results.md](test-results.md). Scope boundaries —
what is deliberately *not* tested — in [test-strategy.md](test-strategy.md).

### 13. Company settings can be stored and edited *(inferred)*

`/Settings` edits company identity, address, tax identifiers, document text, accent colour and
logo. Values persist as `CompanySetting` key/value rows and are mirrored onto the `Company`
columns. Verified manually: changed City and PIN, reloaded a fresh page, values persisted; a second
save updated rather than duplicated rows (10 rows before and after). Covered by 18 service tests.

### 14. A quotation can be exported as a PDF *(inferred)* — **with caveat**

`/Quotations/Pdf/{id}` returns a valid PDF (`%PDF-1.4`, 54 KB, correct filename and content type),
with the company logo embedded in the header. Deleting the logo file and regenerating produced a
valid PDF with zero embedded images and no error, proving graceful degradation.

> **Caveat: the PDF layout has never been visually inspected.** No PDF renderer is available in the
> development environment (`pdftoppm` absent; the preview browser's plugin renders blank), so
> verification was structural — valid header, byte size, embedded-image count, no exceptions. A
> human should open one and confirm the layout reads correctly before this is considered signed off.

### 15. A dashboard shows live counts from the database *(inferred)*

Four KPI cards — products, customers, quotations today, quotations expiring within 7 days — each a
database `COUNT` scoped to the company. All four were cross-checked against direct SQL (8 / 7 / 1 /
0), then boundary-tested by inserting quotations at 3 days, 8 days and already-lapsed, which moved
the figures to the expected 3 and 1. Covered by 13 service tests.

### 16. Data is isolated per company on both read and write paths *(inferred)*

Added after a **security-relevant defect was found in final review**: `CompanyId` was a bindable
property on the product request DTOs, so a signed-in user could create a product under another
company, or move an existing one via the hidden field on the edit form. `GetByIdAsync` and
`DeleteAsync` performed no company check at all.

Fixed by removing `CompanyId` from the request DTOs entirely and making the tenant an explicit
service argument. Verified against the running application:

| Attack | Result |
| --- | --- |
| `CompanyId` inputs present on the create form | **0** — the field no longer exists |
| POST with `CompanyId=99999999-…` forced into the request body | Product created under the **signed-in user's** company |
| Records written under the injected company id | **0** |
| `GET /Products/Details/{another-tenant-id}` | **404** |
| `GET /Products/Edit/{another-tenant-id}` | **404** |

| Path | Isolated | Tested |
| --- | --- | --- |
| List and search (products, customers, quotations) | Yes | Yes |
| Read by id | Yes | Yes — *not true before this fix* |
| Create | Yes | Yes |
| Update / delete | Yes | Yes — and ownership cannot be reassigned |

Covered by 12 tests in `Acceptance/WriteTenantIsolationTests.cs`, symmetric to the existing
read-path tests. Full account in `ai-prompts/debugging.md` #10.

**Remaining limitation:** there is no global database query filter on `CompanyId`, so the guarantee
rests on every service scoping its own queries rather than on the data layer.

## Not covered by any criterion

Delivered but with **no acceptance criterion**, stated or inferred, because they are known gaps
rather than finished behaviour:

| Gap | Where recorded |
| --- | --- |
| **No global database query filter on `CompanyId`** — isolation depends on every service scoping its own queries. Reads and writes are both isolated and tested (see criterion 16), but the guarantee is by convention rather than structural | `data-model.md`, `requirements-analysis.md` |
| No customer detail page — search results link to the list instead | `ui-flow.md` |
| Quotations have no status lifecycle, and cannot be edited or deleted | `README.md` |
| Quotation-number allocation has a read-then-write race | `api-contract.md` |
| Roles are seeded but no controller restricts by role | `README.md` |
| Registration, password reset and user management do not exist | by design — Core scope |
