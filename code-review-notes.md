# Code Review Notes

Findings from reviewing the codebase, and what was done about them.

## AI-Assisted Review Summary

Review was **not** a distinct phase in this project — there was no single "review the codebase" pass,
and `ai-prompts/code-review.md` is consequently near-empty. Findings surfaced in two ways instead:

1. **In-step, by the AI itself**, usually while writing a test or implementing an adjacent feature.
   The `LogoStorage` contract gap and the `Company`-columns drift both came out this way.
2. **By me reading the documentation**, which is how the one genuine vulnerability was found.

That split is the honest headline. The AI was good at catching *contract* problems — a layer
trusting another layer's failure modes, two write paths that could disagree — because those are
visible in the code it was actively editing. It did not catch the security defect, and it had in
fact documented that defect early and then carried the note forward for ten steps without acting on
it.

The findings below are in the order they were found.

---

## 2026-07-18 — Trust boundary: extension and `Content-Type` are client-supplied

**Finding, raised by the AI during the logo upload step.** The prompt asked for an image upload
limited to png/jpg with a size cap. Validating on file extension and `Content-Type` would have met
the request and been wrong: **both are supplied by the client.** A renamed text file passes both.

**Action:** validation checks the file's **magic bytes** as well, and this was demonstrated rather
than asserted — a renamed text file was rejected by the signature check alone, recorded in the
verification table in `ai-prompts/implementation.md`.

Two related decisions came from the same reasoning:

- **GUID filenames**, because a client-supplied filename can contain traversal segments or overwrite
  an existing file.
- **Delete the old file only after the new save succeeds**, so a failed save never leaves the
  database pointing at a file that no longer exists.

**My observation:** this is the strongest single piece of unprompted reasoning in the build. The
useful part was not the magic-byte check itself but the framing — asking which inputs are attacker
controlled, and then not trusting those. I'd have specified the extension check and stopped.

---

## 2026-07-18 — Two write paths that could disagree: the `Company` columns drift

**Finding.** Company details existed in two places: as columns on the `Company` entity, and as rows
in the `CompanySetting` key/value store. The settings screen wrote only to the settings rows, so the
two stores could drift apart, with no rule about which one won.

**Notable because the AI flagged it and then explicitly declined to fix it unilaterally** — it was
raised as a decision needing input ("settle it before the PDF work, or they will drift"), since
picking a winner between two stores is a design call, not a cleanup.

**Action after I confirmed the direction:** `UpdateSettingsAsync` now mirrors writes onto the
`Company` columns, so there is one code path rather than two that can drift.

**My observation:** the right call was surfacing it rather than silently picking. The wrong version
of this is an assistant that quietly makes a schema-shaped decision inside a feature step.

---

## 2026-07-18 — A layer trusting another layer's exception contract

**Finding, produced by a failing test the AI wrote for itself.**
`An_unreadable_logo_does_not_fail_the_document` failed on first run.

`LogoStorage.TryReadAsync` catches IO and permission errors and returns `null`. But
`QuotationPdfService` was **trusting** that contract — a storage implementation throwing anything
outside that set would have failed the entire document. That is precisely the failure the prompt had
asked to prevent, and it was invisible while only one storage implementation existed.

**Action:** added a catch in the PDF service as well, so the guarantee holds regardless of what any
future `ILogoStorage` does. Warnings are logged at **both** layers, so a dangling path is
discoverable rather than silent.

**Principle it settled on:** a decorative image must never be able to fail a commercial document.
Degrade, never fail.

**My observation:** the value here came from the test being written to a *stated requirement* rather
than to the implementation. A test written against the code as it stood would have mocked
`TryReadAsync` returning null and passed.

---

## 2026-07-18 — Dead code: `IPdfGenerator` removed

**Finding:** `InventoryErp.Application/Interfaces/IPdfGenerator.cs` was unused. Nothing implemented
it, nothing injected it, and it was never registered in DI. A repository-wide search found exactly
one code reference: its own declaration.

**Origin:** created during the initial solution scaffold as a placeholder, at a point when PDF
generation was a known future requirement but no library had been chosen. It was documented at the
time as "no implementation registered and injecting this will fail until one is added" — accurate,
but that state persisted for the whole project.

**Superseded by** `IQuotationPdfService`, built when PDF generation actually landed.

### Why the placeholder was not implemented

The two signatures are the substance of the finding:

```csharp
// The placeholder — removed
Task<byte[]> RenderAsync(string templateName, object model, CancellationToken ct = default);

// What is actually used
Task<ServiceResult<GeneratedDocument>> GenerateAsync(Guid quotationId, Guid companyId, CancellationToken ct = default);
```

| Aspect | `IPdfGenerator` | `IQuotationPdfService` |
| --- | --- | --- |
| Template selection | `string templateName` — a typo compiles and fails at runtime | No template parameter; the method *is* the document |
| Input | `object model` — no compile-time guarantee the model matches the template | Typed ids; the service loads what it needs |
| Output | `byte[]` — the caller must know the content type and invent a filename | `GeneratedDocument` carries filename, content type and bytes together |
| Failure | Exceptions only | `ServiceResult`, consistent with every other service in the codebase |

The `(string, object)` pairing is the core problem: it is a **stringly-typed** contract where the
compiler cannot check that the named template and the supplied model agree. Both mistakes surface
only at runtime, in the code path that produces a customer-facing document.

Its generality was also unearned. It was designed for "many document types share one renderer", but
only one document type exists, and a second (an invoice) would want its own typed inputs rather
than another `object`. The abstraction predicted a requirement that never arrived in that shape.

**Action:** deleted. Build clean with zero warnings, 121/121 tests passing — confirming nothing
depended on it.

### Lesson

A placeholder interface asserting a design decision that has not been made yet is a liability, not
a head start. This one named a rendering strategy (string-keyed templates) roughly a dozen steps
before the library was chosen, and the eventual design went a different way. Interfaces are cheapest
to write **after** there is a caller and an implementation to shape them.

Documentation referencing it — `README.md`, `implementation-plan.md`, `design-notes.md`,
`tool-workflow.md`, `ai-prompts/*` — is historically accurate at the point it was written, so it was
left alone rather than rewritten; `design-notes.md` already records why the placeholder was not
implemented. Only `README.md`'s "Known gaps" entry described a *current* state and was updated.

---

## 2026-07-19 — Tenant isolation: `CompanyId` accepted from client input

**The one genuine vulnerability in the build, and the only finding here not surfaced by the AI.**

**Found by:** me, reading `acceptance-criteria.md` — not by a test, a tool, or a review pass.

**Finding.** `CompanyId` was a bindable property on `CreateProductRequest` and
`UpdateProductRequest`, so ASP.NET model binding populated it from whatever the client posted. A
signed-in user could create a product owned by another company, and — via the hidden field on the
edit form — move an existing product into one.

Investigation found **three further holes that had never been documented anywhere**:
`GetByIdAsync`, `UpdateAsync` and `DeleteAsync` took no company argument at all. Any product could
be read, edited or deleted by id, from any tenant.

**Why it survived ~10 build steps:** nothing failed. The app worked, every feature demo passed, and
the suite was green throughout. The gap had also been *documented* early — Entry 5 of
`implementation-plan.md` flagged "`CreateProductRequest.CompanyId` is a user-editable form field" —
and then restated in file after file without anyone acting on it. Writing it down substituted for
fixing it.

**Action:**

- Removed `CompanyId` from the request DTOs **entirely**, so there is no longer a property to bind.
  Because the property was deleted rather than ignored, the compiler located all ~20 call sites —
  nothing depended on remembering to check.
- Made the tenant an explicit argument on create, update, get-by-id and delete, resolved server-side
  from the signed-in user via `ICurrentCompanyProvider`.
- `UpdateAsync` never reassigns `CompanyId`, so a record cannot be moved between tenants.
- 12 tests in `Acceptance/WriteTenantIsolationTests.cs`.
- Verified by **reproducing the attack** against the running app: POSTing a forged `CompanyId`
  created the product under the real company, zero records landed under the injected id, and foreign
  ids returned 404 on Details and Edit.

**A mistake made during the fix, recorded because it matters more than the fix.** A bulk `sed`
stripped `CompanyId` from *entity* initialisers in tests as well as from DTOs. Four tests failed
loudly and were corrected. **Two others kept passing while testing nothing** — settings attached to
`Guid.Empty`, asserting behaviour about a company that did not exist. The suite went green and
proved nothing.

**My observation:** the AI's first response to this finding was to propose a **documentation**
change — updating the notes to describe the gap more accurately. I had to reject that explicitly and
ask for a code fix. That instinct is worth watching for: when a defect is already written down
somewhere, there's a pull toward improving the write-up instead of closing the hole.

---

## 2026-07-19 — An index that looked like a constraint

**Finding, surfaced by a question rather than a review.** Asked whether the `CompanyId` fix needed a
database migration, the answer was no — it changed DTOs, services and views only. But checking the
schema in order to *prove* that turned up something else: **no `CompanyId` column had a foreign key
at all.** Only three domain FKs existed in the whole database.

**Cause.** The domain entities deliberately have no navigation properties, so EF infers no
relationship and one exists only if written by hand. `HasOne<Company>()` never was. The four
tenant-scoped tables carried an *index* on `CompanyId` — which reads as reassuring in a schema dump
— with nothing behind it.

**Why nothing caught it:** 185 tests passed identically before and after the constraints were added,
because the EF in-memory provider ignores foreign keys entirely.

**Action:** configured the relationship on all four tenant-scoped entities with `Restrict`, in
migration `AddCompanyForeignKeys`. Verified against real SQL Server — a forged `CompanyId` insert is
rejected with error 547, and `DELETE FROM Companies` is blocked while records exist.

**Scope, stated honestly:** this stops *forged and orphan* tenant ids. It does not stop one real
company's id reaching another's rows — the FK is satisfied either way. Tenant scoping remains
application-only.

---

## Changes Made After Review

| Finding | Change |
| --- | --- |
| Client-supplied extension / `Content-Type` | Magic-byte validation, GUID filenames, delete-after-successful-save |
| `Company` columns vs settings rows drift | `UpdateSettingsAsync` mirrors writes onto the `Company` columns — one path, not two |
| PDF service trusting `ILogoStorage`'s exception contract | Defensive catch added in `QuotationPdfService`; warnings logged at both layers |
| `IPdfGenerator` dead code | Deleted; `README.md` known-gaps entry updated |
| `CompanyId` bindable from client input | Removed from request DTOs; tenant an explicit server-resolved argument on all four paths; 12 tests; attack reproduced and confirmed neutralised |
| No FK on any `CompanyId` column | `AddCompanyForeignKeys` migration, `Restrict` on all four tables; verified against real SQL Server |
| Docs contradicting each other on isolation status | Repo-wide sweep across `data-model.md`, `acceptance-criteria.md`, `README.md`, `debugging-notes.md`, `test-results.md`, `design-notes.md` |

## Suggestions Rejected (and why)

Genuine rejections from the record — several are the AI declining to expand scope, which is worth
distinguishing from me overruling it:

- **Deleting the overlapping tests in `QuotationServiceTests` / `SettingsServiceTests`** when the
  `Acceptance/` folder was added. They assert different things — invariants and per-method behaviour
  rather than fixed expected values — and removing working coverage to avoid *apparent* duplication
  would be a net loss.
- **An exhaustive GST rounding sweep.** The existing calculator tests cover all four Indian slabs
  plus an explicit midpoint case, and the header-equals-sum-of-lines invariant catches drift without
  enumerating fractions. Recorded in `test-strategy.md` rather than left implied.
- **Enforcing logo dimensions at upload time.** Constrained at *render* time instead (180 × 60 pt),
  so the original file stays intact and a future document layout can choose its own box.
- **Image resizing / re-encoding on upload.** Needs an imaging dependency, and QuestPDF already
  scales cleanly.
- **Storing the logo image in the database.** A path keeps backups small and lets the web server
  serve the file directly.
- **Implementing the `IPdfGenerator` placeholder.** Its `RenderAsync(string templateName, object
  model)` signature is stringly-typed — neither the template name nor the model shape is
  compiler-checked, and both fail at runtime in the code path that produces a customer-facing
  document. Rejected in favour of a typed `IQuotationPdfService`, and the placeholder was later
  deleted outright.
- **Integration and UI tests.** Out of Core scope. Documented as a gap rather than silently omitted
  — and in hindsight this is the rejection I'd most want to revisit, since the in-memory provider is
  what allowed the missing foreign keys to go unnoticed.

**One rejection went the other way — me overruling the AI:** its initial response to the `CompanyId`
finding was a documentation update rather than a code change. Rejected; a real fix was required.

