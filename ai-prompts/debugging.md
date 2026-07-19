# Debugging Prompts

---

## 2026-07-18 — Client-supplied `CompanyId` (tenant isolation defect)

**Tool:** Claude Code (Opus 4.8). **Severity: security-relevant.** Found during final review, not by
a failing test.

### Prompt

> REAL FIX NEEDED, not a docs issue: the notes say CompanyId is a user-editable field on the product
> create form, meaning a signed-in user can currently write a product under a different company's
> CompanyId. Fix this — CompanyId on create (Product, and check Customer and Quotation creation too)
> must be derived server-side from the signed-in user's company via ICurrentCompanyProvider, never
> taken from client input, even if the form doesn't currently render a visible field for it (check
> for a hidden field or an unvalidated bound property too).
>
> Add a test proving this: attempt to create a record while asserting/forcing a different CompanyId
> than the current user's, and confirm the record is created under the correct company regardless.

### What was found — worse than reported

The reported issue was real, and investigation found **four related holes**, three of which had
never been documented:

| # | Hole | Consequence |
| --- | --- | --- |
| 1 | `CreateProductRequest.CompanyId` bound from the form | Create a product owned by another company |
| 2 | `UpdateProductRequest.CompanyId` posted as a **hidden field**, and `UpdateAsync` assigned it | **Move an existing product into another company** |
| 3 | `GetByIdAsync(id)` took **no** company argument | Read any product by id, including another tenant's |
| 4 | `DeleteAsync(id)` took **no** company argument | Soft-delete any product by id |

Numbers 2–4 were not in the prompt. Number 3 also **contradicted the project's own documentation**,
which claimed reads were tenant-isolated — true for list and search, false for lookup by id.

Checked and found **safe**: `QuotationsController` already set `CompanyId` from the provider;
`ICustomerService` is read-only and scoped; settings, dashboard and search all take the company as
an argument.

### Root cause

The DTO carried the tenant. Once `CompanyId` is a bindable property on a request model, ASP.NET
model binding will populate it from any posted value — the visible form field was a symptom, not the
cause. Removing the visible input alone would have left the hole open to a crafted POST.

The id-based methods had a different cause: they were written before the tenant argument existed on
the list methods, and were never revisited when it was added.

### The fix

1. **Removed `CompanyId` from `CreateProductRequest` and `CreateQuotationRequest` entirely.** There
   is no property to bind, so the attack cannot be expressed. This is the structural fix; everything
   else is defence in depth.
2. Made the tenant an **explicit argument** on every affected method:
   `CreateAsync(companyId, request)`, `UpdateAsync(companyId, request)`,
   `GetByIdAsync(id, companyId)`, `DeleteAsync(id, companyId)`,
   `CreateQuotationAsync(companyId, request)`.
3. `UpdateAsync` **never reassigns** `CompanyId` — a record cannot be moved between tenants.
4. All four id-based paths now return `NotFound` for another tenant's record, so an id cannot be
   probed for existence.
5. Removed the visible field from `Create.cshtml` and the hidden one from `Edit.cshtml`.

Because the DTO property was deleted, the **compiler** located every call site — 20-odd across
controllers and tests. Nothing depended on remembering to check.

### Verification

12 new tests in `Acceptance/WriteTenantIsolationTests.cs`, symmetric to the existing read-path
tests. Then the attack was reproduced against the running application:

| Check | Result |
| --- | --- |
| `CompanyId` inputs on the create form | **0** |
| POST with `CompanyId=99999999-…` injected into the body | Product created under the **real** company |
| Records under the injected company id | **0** |
| `GET /Products/Details/{foreign-id}` | **404** |
| `GET /Products/Edit/{foreign-id}` | **404** |
| Full suite | **185 passing** (was 173) |

### A mistake made while fixing it

The bulk edit that removed `CompanyId` from request DTOs also stripped it from **entity**
initialisers in the tests. Four tests failed immediately — but two others still *passed* while
silently no longer testing anything, because they attached settings to `Guid.Empty` instead of the
company under test. Both were restored.

Worth recording: a passing test suite did not prove the edit was correct. The two silently-weakened
tests would have stayed green indefinitely.

### Lesson

**Do not put the tenant on the request model.** A bindable `CompanyId` is an authorisation decision
delegated to the HTTP client. The safe shape is for the tenant to enter the call stack once, from
the authenticated principal, and be passed explicitly — where its absence is a compile error rather
than a silent default.

The wider gap remains: scoping is enforced by every service remembering to filter, not by the
database. A global EF query filter on `CompanyId` would make this class of bug structurally
impossible. That is recorded in `requirements-analysis.md` and `data-model.md`.

---

## Earlier work

No prompt before this one was dedicated to debugging.

Problems encountered so far arose during implementation and were diagnosed and fixed within those
steps. Seven are recorded with cause and fix in [debugging-notes.md](../debugging-notes.md):

1. `CS0509` — deriving from a sealed DTO
2. `AddDefaultUI()` unavailable in Infrastructure
3. `dotnet new sln` producing `.slnx`, not `.sln`
4. `dotnet ef` requiring the Design package in the startup project
5. Migration name collision with a stale `InitialCreate`
6. `QUOTED_IDENTIFIER` failures against filtered indexes
7. Database created on the wrong SQL Server instance

The last three are the ones most likely to recur.
