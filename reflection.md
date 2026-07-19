# Reflection

> Written at the end of the build, drawing on `ai-prompts/`, `debugging-notes.md`, the git history
> and the actual sequence of prompts. Specific incidents are cited so claims here can be checked
> against the record rather than taken on trust.

## What I Built

An SME ERP / inventory management application on .NET 10, Clean Architecture, six projects with the
dependency rule enforced by project references rather than convention — `InventoryErp.Domain` has no
package or project references at all.

Core scope delivered:

- **Domain model** — `Company`, `CompanySetting`, `Product`, `Customer`, `Quotation`,
  `QuotationLine`, all soft-deleted and audit-stamped via `BaseEntity`.
- **Auth** — ASP.NET Identity with `Guid` keys, cookie auth, deny-by-default fallback policy, seeded
  admin. No registration or password reset, deliberately.
- **Products** — full CRUD with per-tenant SKU uniqueness and paged search.
- **Customers** — list only.
- **Quotations** — server-side GST calculation (discount before tax, Indian slabs), stored totals,
  and PDF export via QuestPDF.
- **Settings** — key/value store plus company profile fields and logo upload.
- **Global search** and a **4-KPI dashboard** over real data.
- **185 tests**, `dotnet build` clean with warnings-as-errors on.

Fourteen commits, roughly one per build step.

## How I Used AI (across the lifecycle)

One prompt per build step, and documentation updated **in the same step as the code** — not batched
at the end.

That second part mattered more than I expected. Because `implementation-plan.md`, `data-model.md`
and the `ai-prompts/` archive were written while each step was fresh, they record the reasoning as
it actually was, including decisions that later turned out wrong. `ai-prompts/implementation.md` ran
to ~1,600 lines by the end, with an **Accepted / Changed beyond the request / Rejected / Open items**
breakdown per step. The "Rejected" sections turned out to be the most useful thing in the repo when
writing this up — they're the only place the roads not taken are recorded.

The cost is real: each step took noticeably longer, and there was constant pressure to skip the doc
update and move on. I mostly didn't, and the one category where I did let things slide — keeping
*current-state* claims consistent across files — is exactly where the documentation broke (below).

## What AI Helped With Most

Three cases where the AI produced something better than what I'd asked for, and I could tell it was
better:

**1. The magic-byte reasoning on logo upload.** I asked for a file upload with a size limit and
png/jpg restriction. What came back also validated the file's **magic bytes**, with the reasoning
that extension and `Content-Type` are *both* client-supplied — a renamed text file passes both. That
was demonstrated, not asserted: the verification table shows a renamed text file rejected by the
signature check alone. It also used GUID filenames (a client filename can carry traversal segments)
and deleted the old file only *after* a successful save. I hadn't specified any of that.

**2. The `LogoStorage` exception-contract gap — found by a test the AI wrote for itself.** The test
`An_unreadable_logo_does_not_fail_the_document` **failed on first run**. `LogoStorage.TryReadAsync`
caught IO and permission errors, but `QuotationPdfService` was *trusting* that contract — any other
exception type would have failed the entire document, which was precisely what my prompt asked to
prevent. A catch was added at the PDF layer too. The principle it landed on is the right one: a
decorative image must never be able to fail a commercial document.

**3. Settings-defaults test coverage.** I asked for a partial-settings resilience test. What came
back was five, because "partial settings" is not one path — absent rows, blank values, a malformed
colour and a missing logo file all reach the renderer differently. It also added a **precondition
test** (`No_setting_rows_exist_for_the_company`) asserting the fixture is genuinely empty, without
which the defaults test could pass for entirely the wrong reason. That's a subtlety I would not have
thought to ask for.

The through-line: it was strongest when a decision had a *stateable rationale* — trust boundaries,
contracts between layers, what a test actually proves.

## What AI Got Wrong

**1. The `CompanyId` write-path tenant isolation vulnerability.** The most serious defect in the
build, and it survived roughly ten build steps.

`CompanyId` was a bindable property on `CreateProductRequest` / `UpdateProductRequest`, so ASP.NET
model binding populated it from whatever the client posted. Investigation found three further holes
that had never been documented anywhere: `GetByIdAsync`, `UpdateAsync` and `DeleteAsync` took **no
company argument at all**. Any product could be read, edited, moved between companies, or deleted by
id.

Two things about this are worth sitting with. First, **nothing failed** — the app worked, the suite
was green, and the feature demos passed. Second, **the AI had documented the gap early and then
stopped believing it**: Entry 5 of `implementation-plan.md` flagged "`CreateProductRequest.CompanyId`
is a user-editable form field" as a known gap, and it simply carried forward, restated in file after
file, without anyone acting on it. It was ultimately caught by me reading `acceptance-criteria.md`,
not by any test or tool.

The fix removed the property from the DTOs entirely and made the tenant an explicit argument
everywhere. Because the property was *deleted* rather than ignored, the compiler located all ~20 call
sites — nothing depended on remembering to check.

**2. The mistake made while fixing it.** Mid-fix, a bulk `sed` stripped `CompanyId` from *entity*
initialisers in tests as well as DTOs. Four tests failed loudly and were fixed. **Two others kept
passing while silently testing nothing** — settings attached to `Guid.Empty`, asserting behaviour
about a company that didn't exist. A green suite after a bulk edit proved nothing at all. This is the
one I'd most want to remember.

**3. `data-model.md` and `acceptance-criteria.md` contradicted each other on tenant isolation.** One
said isolation was enforced and tested; the other said it was unenforced and `CompanyId` was a
user-editable field. Both were AI-written, a few steps apart, and neither was wrong *when written* —
the second simply wasn't revisited. Reconciling them took a repo-wide sweep, and even then the first
pass missed the **status banner** at the top of `README.md` while correctly fixing the known-gaps
entry 150 lines below it, so `README.md` briefly contradicted *itself*. A later sweep also found
`test-results.md` still asserting isolation was "untested" — flatly false by then, with 12 dedicated
tests passing.

**4. Presupposing work that didn't exist.** Three separate times I prompted for a follow-up to the
PDF service, and three times the PDF service hadn't been built yet — the AI had to stop and say so
rather than proceed. Related: at final review it initially tried to answer the `CompanyId` issue with
a **documentation change** when what was needed was a code fix, and I had to push back explicitly.

**5. Verification that checked the wrong thing.** Every page rendered completely unstyled after the
login step, because the global `FallbackPolicy` applied to `MapStaticAssets` endpoints and every CSS
and JS request was 302-redirected to the login page. This shipped unnoticed because verification had
checked page *text* and never looked at asset status codes or a rendered screenshot. I found it from
a screenshot.

**6. Case-sensitive search**, masked in manual testing by SQL Server's default case-insensitive
collation and caught only by an in-memory-provider test — an instance of the test environment being
*stricter* than production and doing me a favour.

**7. Documentation that failed on a clean checkout** — `README.md` was missing a `dotnet restore`
step (so `dotnet ef database update` died with `NETSDK1004`), told the reader to "register a user"
after registration had been removed, and claimed 173 tests when there were 185.

**8. An index that looked like a constraint.** Right at the end, checking whether the `CompanyId` fix
needed a migration turned up that **no `CompanyId` column had a foreign key at all**. The entities
have no navigation properties, so EF inferred no relationship and none was written by hand. The
tables had an *index* on `CompanyId` — which reads as reassuring in a schema dump — with nothing
behind it. Nothing failed, and 185 tests passed identically before and after adding the constraints,
because the in-memory provider ignores foreign keys entirely.

The pattern across 1, 3, 5 and 8: **the failures were all silent.** Not one produced an error
message. Everything that went wrong went wrong quietly, and was found by reading or by looking at
the actual artefact — never by the tooling.

## How I Validated AI Output

I did not treat the assistant's own summary of its work as evidence. The pattern that emerged:

- **Direct SQL through `sqlcmd`**, not just the app's own reads — confirming `CreatedBy` was really
  stamped, that cascade delete removed a quotation's lines (1 → 0), that restrict blocked deleting a
  customer holding quotations, and at the end that a forged `CompanyId` insert is rejected with
  error 547.
- **Restart-persistence checks** — data survived an application restart, and seeding was genuinely
  idempotent rather than merely appearing so.
- **Live-settings PDF check** — generating a PDF, then changing settings and regenerating to confirm
  the change actually reached the document.
- **The deleted-logo-file check**, which is the one I'd point at as the model for the rest. Rather
  than accepting a 200 response, verification deleted the logo from disk leaving a dangling DB path,
  regenerated, and asserted the **embedded image count dropped from 1 to 0** — proving the logo was
  genuinely omitted rather than rendered blank or as a broken placeholder. Then restored the file and
  confirmed the output was byte-identical to the original.
- **Screenshots and asset status codes**, after the unstyled-app incident made it clear that
  text-based checks confirm a route works and say nothing about whether the page is usable.
- **Reproducing the attack** for the tenant-isolation fix — POSTing a forged `CompanyId` against the
  running app and confirming zero records landed under the injected id.

Where I couldn't verify, I recorded it: the PDF layout has **never been visually inspected**
(`pdftoppm` isn't installed and the preview browser renders PDFs blank), so verification there is
structural — valid header, byte size, embedded image count — not visual. That's written down as a
limitation rather than quietly omitted.

## What I Would Improve Next

**Treat a known gap with an expiry date.** The `CompanyId` vulnerability was *documented* about ten
steps before it was fixed. Writing it down created a feeling of having handled it. A gap that
describes a security hole should block the next step, not get carried forward in a list.

**Never trust a green suite after a bulk edit.** The `sed` incident is the clearest lesson here:
two tests kept passing while testing nothing. After any mechanical multi-file change I'd want to
confirm the tests still *fail when they should* — invert an assertion and check it goes red.

**Get integration tests against real SQL Server in early.** The in-memory provider ignores filtered
indexes, foreign keys, column lengths and decimal precision. It didn't just fail to catch the missing
`CompanyId` foreign keys — it produced 185 passing tests that actively suggested the schema was
fine. It's misleading rather than merely incomplete, and everything it can't see had to be checked by
hand.

**Separate current-state docs from historical ones, structurally.** Most of the documentation drift
came from files mixing "what is true now" with "what was true at step 5". Dated journal entries
should stay unedited; current-state claims need a single home that gets swept. I ended up doing that
sweep by grep, repeatedly, and still missed the README banner on the first pass.

**Ask for the attack, not the feature.** "Add products with multi-tenancy" produced isolation that
looked right. "Show me a signed-in user reading another company's product by id" would have found the
hole immediately. The prompts that produced the best work were the ones that named a failure mode.

## Reusable Workflow

Yes — with one addition.

The core pattern holds: **one prompt per build step, documentation updated in the same step, and an
Accepted / Changed beyond the request / Rejected / Open items breakdown recorded per step.** The
"Changed beyond the request" sections repeatedly surfaced things I hadn't asked for and wanted
(mirrored `Company` column writes, gitignoring the uploads directory, a "remove logo" checkbox
without which an uploaded logo could never be cleared). The "Rejected" sections meant decisions
didn't have to be re-litigated later.

What I'd add is a **standing adversarial step**: after each feature, one prompt whose only job is to
attack what was just built — not "does it work" but "who can reach this who shouldn't". Every serious
defect in this project was silent, and none of them were found by the step that built the feature.
