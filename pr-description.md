# SME ERP / Inventory Management — Core scope

## Summary

An SME ERP / inventory management application on .NET 10 using Clean Architecture, built
incrementally over fourteen commits. Six projects with the dependency rule enforced by project
references rather than convention — `InventoryErp.Domain` has no package or project references at
all.

Two patterns shape most of the code:

- **`ServiceResult<T>` instead of exceptions.** Application services return a result carrying a
  `ResultStatus` (`Success`, `NotFound`, `ValidationFailed`, `Conflict`, `Unauthorized`, `Error`).
  Exceptions are reserved for genuinely unexpected faults; the web layer maps statuses onto HTTP
  responses.
- **Fluent API only, no EF attributes on domain classes**, which is what keeps `Domain`
  dependency-free.

`dotnet build` is clean with warnings-as-errors enabled. **185 tests passing.**

## Features Implemented

| Area | What's included |
| --- | --- |
| **Domain** | `Company`, `CompanySetting`, `Product`, `Customer`, `Quotation`, `QuotationLine` — all soft-deleted and audit-stamped via `BaseEntity` |
| **Auth** | ASP.NET Identity with `Guid` keys, cookie auth, deny-by-default fallback policy, seeded admin. No registration or password reset, by design |
| **Products** | Full CRUD, per-tenant SKU uniqueness (filtered unique index, so a soft-deleted SKU can be reused), paged search, reorder-level flagging |
| **Customers** | List only |
| **Quotations** | Multi-line create, server-side GST calculation, PDF export via QuestPDF |
| **Settings** | Key/value store plus company profile fields, with logo upload |
| **Global search** | Top-nav, minimum 2 characters, across products, customers and quotations |
| **Dashboard** | 4 KPI cards over real data, plus a low-stock list |

### Notable implementation decisions

- **GST is charged on the discounted amount, not the gross** — discount applies before tax, per
  Indian GST practice. Rounded to 2dp with `MidpointRounding.AwayFromZero`, and header totals are
  sums of already-rounded line figures so the stored header reconciles exactly with the stored
  lines.
- **Quotation totals are stored, not computed.** Each line copies `UnitPrice` and `GstPercent` from
  the product at quoting time — a quotation is a commercial document and must keep the figures it
  was issued with, even after prices change.
- **Settings are key/value rows**, so a new setting needs no schema change, with writes mirrored
  onto the `Company` columns to keep a single source of truth.
- **Logo upload validates magic bytes**, not just extension and `Content-Type` — both of those are
  client-supplied and a renamed text file passes both.
- **A missing logo degrades rather than fails.** A quotation that cannot be sent because a logo
  moved is far worse than one sent without a logo.
- **Tenant isolation is server-derived.** `CompanyId` is not present on any request DTO, so it
  cannot be model-bound from client input; it is resolved from the signed-in user and passed
  explicitly to every service method. Backed by foreign keys on all four tenant-scoped tables.

## Known Limitations

Stated plainly rather than left to be discovered. These are delivered gaps, not work in progress —
each is recorded in the file named.

| Limitation | Recorded in |
| --- | --- |
| **No global database query filter on `CompanyId`.** Reads and writes are both isolated and tested, and the database rejects forged or orphan tenant ids via foreign keys — but *scoping between two real companies* depends on every service filtering its own queries. Structural rather than by convention is the right end state | `data-model.md`, `requirements-analysis.md` |
| **Search has no relevance ranking.** Results are grouped by entity type, not scored — an exact SKU match sorts no higher than a loose name match | `ui-flow.md`, `test-strategy.md` |
| **No customer detail page.** Search results and the customer list have no per-record view, so customer results link to the list instead (handled explicitly in `SearchResultRoutes.HasDetailPage`) | `ui-flow.md` |
| **Quotations have no status lifecycle** (draft / sent / accepted) and cannot be edited or deleted. The detail view infers "Expired" from `ValidUntil` alone | `README.md` |
| **Quotation-number allocation has a read-then-write race.** Not atomic; the filtered unique index is the real guarantee, with five retries before returning `Conflict` | `api-contract.md` |
| **Roles are seeded but no controller restricts by role.** Every authenticated user has full access — role separation is modelled but not enforced | `README.md` |
| **`ICurrentCompanyProvider` resolves the single seeded company.** Correct while one company exists; wrong the moment a second is added. A company claim on `ApplicationUser` is the missing half | `requirements-analysis.md` |
| **Cascade delete does not fire on soft delete.** The database cascade only triggers on a hard `DELETE`, which the application never issues, so soft-deleting a quotation would orphan its lines. Not live — nothing deletes quotations | `README.md` |
| Registration, password reset and user management do not exist | By design — Core scope |

### Testing limitations worth knowing before reviewing

Tests use the **EF in-memory provider**, which ignores filtered unique indexes, foreign keys, column
lengths and decimal precision. So the duplicate-SKU test passes on the service guard, *not* the
database constraint — and the `CompanyId` foreign keys had to be verified by hand against SQL Server
because 185 tests passed identically with and without them. The provider is actively misleading
here rather than merely incomplete. Integration tests against real SQL Server would close the
largest gap.

The **PDF layout has never been visually inspected** — `pdftoppm` isn't available and the preview
browser renders PDFs blank. Verification was structural (valid header, byte size, embedded image
count, no exceptions). Someone should eyeball it before this is considered done.

## Security Notes

No API keys, tokens or passworded connection strings are committed; the connection string uses
Windows integrated authentication. Two things are committed deliberately and must not reach a
deployed environment: the seed admin password (`SeedData.cs`, documented in the README, since there
is no registration) and `TrustServerCertificate=True` in `appsettings.json`.

**QuestPDF is used under the Community licence**, which is free only below a revenue threshold —
this needs revisiting if that changes.

## AI Usage Summary

Built with Claude Code (Opus 4.8), one prompt per build step, with documentation updated in the same
step as the code rather than batched at the end. Every prompt and the reasoning behind each decision
is archived in `ai-prompts/`, structured per step as Accepted / Changed beyond the request /
Rejected / Open items.

AI output was verified rather than trusted: direct SQL through `sqlcmd` rather than the app's own
reads, restart-persistence checks, and for the tenant-isolation fix, reproducing the attack against
the running app. The one genuine vulnerability in the build — `CompanyId` accepted from client input
— was found by reading the documentation, not by any test or tool, and is written up in full in
`debugging-notes.md` #10 and `code-review-notes.md`.

Full detail in `reflection.md` and `final-ai-usage-summary.md`.
