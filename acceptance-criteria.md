# Acceptance Criteria

> **Partial.** Only criteria explicitly stated so far are listed. The full set depends on
> `requirements-analysis.md`, which is still unwritten — no complete functional requirements have
> been provided. This file grows as criteria are stated.

## Core

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

## Not yet stated

Criteria for product, customer, company and quotation management have not been provided. Known
functional gaps that would likely become criteria are tracked in `README.md` and
`implementation-plan.md` — most significantly that tenant isolation is not enforced.
