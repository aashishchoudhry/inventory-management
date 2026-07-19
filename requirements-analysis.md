# Requirements Analysis

> **Provenance — read first.** This is **reverse-engineered from the delivered system and from
> requirements stated in prompts**. It was not transcribed from the project guide, which was never
> shared with the assistant. Treat it as a record of what was built and why, not as the
> authoritative source of requirements. Anyone signing off should reconcile it against the guide.

## Problem

An SME needs to track what it sells, who it sells to, and what it has quoted — replacing
spreadsheets with a single system that holds product, customer and quotation records, keeps GST
calculations correct and consistent, and produces a document that can be sent to a customer.

## Users

One role in practice: a staff member with full access. `Admin`, `Manager` and `StoreKeeper` roles
are seeded but no screen restricts by role, so role separation is **modelled but not enforced**.

## Functional requirements as implemented

| # | Requirement | Delivered |
| --- | --- | --- |
| F1 | Authenticate before reaching any business data | Cookie-based ASP.NET Identity; deny-by-default authorization |
| F2 | Persist data across restarts | SQL Server via EF Core; verified by restart |
| F3 | Maintain a product catalogue with SKU, barcode, price, GST rate and stock | `Product`; full CRUD |
| F4 | List and search products by name, SKU or barcode | Server-side paged search |
| F5 | Maintain customer records | `Customer`; **read-only** — maintenance not in scope |
| F6 | Create a quotation with multiple line items | `Quotation` / `QuotationLine` with a dynamic line editor |
| F7 | Calculate line and header totals server-side | `QuotationCalculator`; discount before GST, half-up to 2dp |
| F8 | Reject invalid quotations with a usable message | `ServiceResult` validation surfaced inline per line |
| F9 | View quotations as a list and in detail | Paged list, newest first, plus a detail view |
| F10 | Export a quotation as a branded PDF | QuestPDF, with company details, logo and accent colour |
| F11 | Configure company details used on documents | Key/value settings with typed access and logo upload |
| F12 | Search across products, customers and quotations from anywhere | Nav dropdown plus a results page, 2-character minimum |
| F13 | Show headline figures on landing | Four KPI cards from database counts |

## Non-functional requirements

| # | Requirement | How it is met |
| --- | --- | --- |
| N1 | Domain logic independent of persistence and UI | Clean Architecture; `InventoryErp.Domain` has zero references, enforced by the build |
| N2 | Expected failures must not be exceptions | `ServiceResult<T>` with an explicit `ResultStatus` |
| N3 | Monetary figures must reconcile exactly | Header totals are sums of already-rounded line figures; asserted by test |
| N4 | Documents must survive missing optional data | PDF renders without logo, terms, footer or address |
| N5 | Usable on a phone | Responsive shell, off-canvas nav, tables scroll within their container |
| N6 | Data must be isolated per company | Every query and write is scoped by `CompanyId` — see below |

## Multi-tenancy: intended vs delivered

The schema is multi-tenant — `Company` owns products, customers, quotations and settings — but the
product only ever runs **one company**, seeded at startup. This gap between the model and the
runtime is the source of the most significant defect found in the project:

- Reads and writes are scoped by `CompanyId`, supplied by `ICurrentCompanyProvider`.
- That provider resolves *the single seeded company*, because nothing links a user to a company.
- Until review, `CompanyId` was also accepted from client input on the product forms — see
  `ai-prompts/debugging.md` #10.

**Before a second company exists**, a company claim must be added to `ApplicationUser`, the provider
must read it, and a global EF query filter should enforce scoping at the data layer rather than
relying on every service remembering to.

## Explicitly out of scope

Recorded so absence is not mistaken for oversight: user registration and management, password
reset, role-based permissions, customer/company create-edit-delete, quotation editing or a status
lifecycle, invoices, purchase orders, stock movements, multi-currency, and any reporting beyond the
four dashboard counts.

## Assumptions

1. Indian GST — four slabs, tax on the post-discount value, `en-IN` money formatting.
2. Quantities are whole units; nothing is sold by weight or length.
3. A quotation does not reserve or decrement stock.
4. One company per deployment.
5. Windows-authenticated SQL Server on the same machine.
