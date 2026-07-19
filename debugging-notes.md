# Debugging Notes

Problems hit during development, their cause, and the fix. Recorded because most were
non-obvious and several will recur.

---

## 1. `CS0509: cannot derive from sealed type 'CreateProductRequest'`

**When:** First build of the scaffold.

**Cause:** `CreateProductRequest` was written as `sealed`, but `UpdateProductRequest` derives from it
to inherit the shared fields and add `Id`.

**Fix:** Removed `sealed` from `CreateProductRequest`.

**Lesson:** Sealing by default is a good habit, but not for DTO base types in an inheritance chain.

---

## 2. `AddDefaultUI()` unavailable in Infrastructure

**When:** Wiring ASP.NET Identity during the scaffold.

**Cause:** `AddDefaultUI()` lives in `Microsoft.AspNetCore.Identity.UI`, an ASP.NET-only package.
Calling it from `InventoryErp.Infrastructure` would have forced a `FrameworkReference` to
`Microsoft.AspNetCore.App` into that layer.

**Fix:** Moved Identity registration to the web composition root (`Program.cs`).
`AddInfrastructure()` registers the DbContext, repositories and services; `Program.cs` registers
Identity and its UI.

**Lesson:** Not everything persistence-adjacent belongs in Infrastructure. The default Identity UI is
a presentation concern.

---

## 3. `dotnet new sln` produced `.slnx`, breaking `dotnet build InventoryErp.sln`

**When:** First build after scaffolding.

**Symptom:** `MSBUILD : error MSB1009: Project file does not exist.`

**Cause:** .NET 10 defaults `dotnet new sln` to the XML-based `.slnx` format. The command referenced
`InventoryErp.sln`, which was never created.

**Fix:** Use `InventoryErp.slnx`. Use `dotnet new sln --format sln` if the classic format is needed
for older tooling.

---

## 4. `dotnet ef` could not find `Microsoft.EntityFrameworkCore.Design`

**When:** Creating the first migration.

**Symptom:** *"Your startup project 'InventoryErp.Web' doesn't reference Microsoft.EntityFrameworkCore.Design."*

**Cause:** The Design package was added to Infrastructure, where the migrations live, but the EF
tools require it in the **startup** project as well.

**Fix:** `dotnet add src/InventoryErp.Web package Microsoft.EntityFrameworkCore.Design`.

---

## 5. `The name 'InitialCreate' is used by an existing migration`

**When:** Generating the migration after all six entities were wired.

**Cause:** The scaffold had already created an `InitialCreate` migration covering only `Product` and
the Identity tables. It was stale — generated before the `Product` rework and the other five
entities — but still occupied the name, and its tables already existed in the database, so
`database update` would also have failed.

**Fix:** Deleted the stale migration files and the model snapshot, dropped the database, then
regenerated and applied a fresh `InitialCreate`. Safe because nothing had shipped and the database
held only throwaway verification rows.

**Lesson:** A migration is only disposable before it reaches an environment you cannot drop. Once
real data exists, the answer is an additive migration instead.

---

## 6. `INSERT failed because the following SET options have incorrect settings: 'QUOTED_IDENTIFIER'`

**When:** Verifying cascade-delete behaviour with `sqlcmd`.

**Cause:** SQL Server requires `QUOTED_IDENTIFIER ON` for any INSERT/UPDATE/DELETE against a table
carrying a **filtered index**. `sqlcmd -Q` defaults it off. Several tables have filtered unique
indexes (`WHERE [IsDeleted] = 0`).

**Fix:** Pass `-I` to `sqlcmd`.

**Note:** The application is unaffected — the .NET SQL client sets `QUOTED_IDENTIFIER ON` by default.
This only bites in ad-hoc command-line sessions. The error message is misleading: it lists many
possible causes and never names the filtered index.

---

## 7. Database created on the wrong SQL Server instance

**When:** After the connection string was changed to `Server=.`.

**Symptom:** The schema existed, but not on the instance the application pointed at.

**Cause:** `dotnet ef` reads the connection string from the **startup project's** `appsettings.json`
at the time the command runs. Every earlier migration command had read `(localdb)\MSSQLLocalDB`, so
that is where the database was created. Changing `appsettings.json` does not move an existing
database.

**Fix:** Ran `database update` again — it targeted `Server=.` and created the schema there — then
dropped the orphaned LocalDB database.

**Lesson:** After changing `DefaultConnection`, verify where the schema actually landed:

```bash
sqlcmd -S "." -I -Q "SELECT name FROM sys.databases WHERE name='InventoryErp';"
```

---

## 8. Every page rendered completely unstyled

**When:** Immediately after the login step added a global authorization fallback policy.

**Symptom:** Serif headings, bulleted navigation, raw browser form controls — despite correct
Bootstrap classes being present in the served HTML, and `bootstrap.min.css` existing on disk at
232 KB.

**Cause:** `MapStaticAssets()` registers endpoints that carry **no authorization metadata**, so the
global `FallbackPolicy` applied to them. Every unauthenticated request for CSS or JS was
302-redirected to `/Account/Login`, and the browser received an HTML login page where it expected a
stylesheet. Confirmed before fixing:

```
/lib/bootstrap/dist/css/bootstrap.min.css   302
/css/site.css                              302
/js/site.js                                302
```

**Fix:** `app.MapStaticAssets().AllowAnonymous();` — after which all return `200 text/css` /
`200 text/javascript`.

**Lesson, and the more important one:** this shipped unnoticed because verification had checked page
*text* via `get_page_text` and never looked at assets or a rendered screenshot. Text-based checks
confirm a route works and content is correct; they say nothing about whether the page is usable. Any
change touching middleware order or authorization now warrants an asset-status check and a
screenshot.

---

## 9. False positive: horizontal overflow at 375px

**When:** Verifying mobile responsiveness of the products table.

**Symptom:** `document.documentElement.scrollWidth` reported 555px against a 375px viewport,
suggesting the page scrolled sideways.

**Cause:** Not a bug. `scrollWidth` counts descendants that overflow inside a clipping container —
the table sits in `.table-responsive` with `overflow-x: auto`, which scrolls internally. The
authoritative check is whether the window can actually scroll:

```js
window.scrollTo(9999, 0); window.scrollX  // → 0, so no horizontal scroll
```

**Lesson:** measure the behaviour a user would experience, not a property that merely correlates
with it.

---

## 10. Client-supplied `CompanyId` broke tenant isolation

**When:** Final review before submission, prompted by a note in `acceptance-criteria.md` rather than
by a failing test.

**Symptom:** none visible. Everything worked; the suite was green.

**Cause:** `CompanyId` was a bindable property on `CreateProductRequest` and `UpdateProductRequest`,
so ASP.NET model binding populated it from whatever the client posted. Investigation found three
further holes that had never been documented: `GetByIdAsync`, `UpdateAsync` and `DeleteAsync` took
**no company argument at all**, so any product could be read, edited, moved between companies, or
deleted by id.

**Fix:** removed `CompanyId` from the request DTOs entirely — there is no longer a property to bind —
and made the tenant an explicit argument on every affected method. Because the property was deleted,
the compiler located all ~20 call sites; nothing relied on remembering to check.

**Verified:** POSTing a forged `CompanyId` now writes under the signed-in user's company, zero
records land under the injected id, and foreign ids return 404 on Details and Edit. 12 tests in
`Acceptance/WriteTenantIsolationTests.cs`.

**Full write-up:** `ai-prompts/debugging.md` #10 — including the mistake made *while* fixing it, where
a bulk edit silently weakened two tests that kept passing.

**Lesson:** never put the tenant on the request model. A bindable `CompanyId` delegates an
authorisation decision to the HTTP client.

---

## 11. `CompanyId` had no foreign key on any table

**When:** Immediately after item 10, prompted by the question *"shouldn't there be a database
migration for this fix too?"*

**Symptom:** none. Nothing failed, and the fix in item 10 was complete and correct.

**Cause:** The answer to the question was "no" — item 10 changed DTOs, service signatures,
controllers and views only, so `dotnet ef migrations add` would have produced an empty `Up()`.
But checking the schema to *prove* that turned up something else: the database had only three
domain foreign keys (QuotationLine→Quotation, QuotationLine→Product, Quotation→Customer). **No
`CompanyId` column had a foreign key at all.**

The reason is structural: the domain entities have no navigation properties, so EF infers no
relationship, and `HasOne<Company>()` had never been configured. The tables had an *index* on
`CompanyId` — which looks reassuring in a schema dump — but nothing behind it. The forged
`CompanyId` from item 10 was rejected by the application; the database would have stored it happily.

**Fix:** configured the relationship on `Product`, `Customer`, `Quotation` and `CompanySetting`
using the same reference-less `HasOne<Company>().WithMany()` overload already used for
Quotation→Customer, with `OnDelete(DeleteBehavior.Restrict)`. Migration `AddCompanyForeignKeys`.

**Verified against real SQL Server**, because the in-memory test provider ignores foreign keys
entirely and all 185 tests passed both before and after:

- Inserting a product with a non-existent `CompanyId` → error 547, constraint named in the message.
- `DELETE FROM Companies` while records exist → error 547, `Restrict` holds.
- Normal writes through the UI (product + quotation `QT-2026-0002`) still succeed, and land with the
  correct server-derived `CompanyId`.

**Scope, stated honestly:** this stops *forged and orphan* tenant ids. It does **not** stop one real
company's id being used to reach another's rows — the FK is satisfied either way. That remains the
application's job, and the global query filter's.

**Lesson:** an index on a foreign-key column is not a foreign key, and a green test suite proves
nothing about constraints when the test provider does not implement them. Also: the question that
found this was aimed at something else entirely. Checking the schema to justify a "no" was what
surfaced the real gap.

---

## Open issues, not yet bugs

Recorded here because they will become bugs when the relevant feature is built:

- **Cascade delete does not fire on soft delete.** `Repository<T>.Remove` issues an `UPDATE`, so the
  database's `ON DELETE CASCADE` never triggers. Soft-deleting a quotation would leave its lines
  visible and orphaned. Any quotation-delete feature must cascade the soft delete in application code.
- **No global query filter on `CompanyId`.** Reads (list, search and by id) and writes (create,
  update, delete) are all scoped by company and covered by tests, and the database now rejects
  forged or orphan tenant ids via foreign keys (item 11) — but *scoping between two real companies*
  still rests on every service filtering its own queries rather than on the data layer. A global EF
  query filter, mirroring the existing soft-delete one, would make the whole bug class structurally
  impossible.

  *This entry previously read "tenant isolation is not enforced… `CompanyId` is a user-editable form
  field". That was true until final review, when the defect was found and fixed — see item 10 below.*
