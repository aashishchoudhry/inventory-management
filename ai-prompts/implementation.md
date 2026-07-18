# Implementation Prompts

A log of prompts given to AI tools during implementation, what the tool did, and what was
accepted, changed or rejected.

---

## 2026-07-18 — Infrastructure: EF Core setup and DbContext

**Tool:** Claude Code (Opus 4.8)

### Prompt

> Now let's start Infrastructure. Add the EF Core 10 SQL Server packages to InventoryErp.Infrastructure,
> and create an InventoryErpDbContext there with just Company and CompanySetting for now — I'll add
> the rest of the entities in the next steps.
>
> Use Fluent API in OnModelCreating for configuration (required fields, max lengths where it makes
> sense) — no attributes on the Domain classes themselves. Don't wire ASP.NET Identity yet, that's a
> separate step.
>
> Then:
> - Update database/setup-notes.md with the database choice (SQL Server LocalDB) and note that
>   DbContext setup has started with Company/CompanySetting
> - Update design-notes.md's Database Design section with a short note on the Fluent API approach and
>   why (keeps Domain clean of EF Core dependencies)
> - Update ai-prompts/implementation.md with this prompt, a summary of what Claude Code did, and what
>   you accepted/changed/rejected
>
> Don't run a migration yet — we'll do that once more entities are wired in.

### What Claude Code did

Before making changes, it checked the existing state and reported three conflicts with the request:

1. **The EF Core packages were already installed** — `SqlServer`, `Design` and
   `Identity.EntityFrameworkCore`, all 10.0.10, added during the initial scaffold. No action needed.
2. **A DbContext already existed.** `ApplicationDbContext` was already present as an
   `IdentityDbContext<ApplicationUser, ApplicationRole, Guid>` with a `Products` DbSet, soft-delete
   query filters and audit stamping, referenced by `Repository<T>`, `UnitOfWork`, `AddInfrastructure`
   and Identity's `AddEntityFrameworkStores`, with an applied migration.
3. **Identity was already wired** to that context, and the login flow had been verified working.

It stopped and asked how to proceed rather than creating a second DbContext over the same database.

After the decision, it:

- Renamed `ApplicationDbContext` → `InventoryErpDbContext`, updating all 8 referencing files and
  renaming the model snapshot file to match.
- Added `Companies` and `CompanySettings` DbSets.
- Created `CompanyConfiguration` and `CompanySettingConfiguration` as `IEntityTypeConfiguration<T>`
  classes, picked up by the existing `ApplyConfigurationsFromAssembly` call in `OnModelCreating`.
- Verified: build clean with 0 warnings, 14/14 tests passing, and `dotnet ef dbcontext info`
  confirming the model loads under the SQL Server provider.
- Wrote `database/setup-notes.md` and the Database Design section of `design-notes.md`.

### Accepted

- **Renaming the existing context** rather than creating a parallel one. Two contexts over one
  database would have meant two migration histories and contested table ownership.
- **`IEntityTypeConfiguration` classes** over inline `builder.Entity<T>()` calls in `OnModelCreating`,
  matching the existing `ProductConfiguration` pattern. This still satisfies "Fluent API, no
  attributes" — `ApplyConfigurationsFromAssembly` is itself called from `OnModelCreating`.
- **Domain-specific max lengths** proposed by the tool: GSTIN capped at 15 and PAN at 10, matching
  the fixed statutory formats, rather than a generic default.
- **A filtered unique index on `(CompanyId, Key)`** for `CompanySetting`, preventing duplicate
  setting keys within a company while allowing a soft-deleted key to be re-added.
- **`CompanySetting.Value` left uncapped**, since settings may hold anything from a boolean flag to
  a block of PDF footer text.

### Changed from the original request

- **"Create an InventoryErpDbContext"** became **rename the existing context**. Taken literally the
  request would have produced a second DbContext alongside `ApplicationDbContext`.
- **"Don't wire ASP.NET Identity yet"** could not be honoured. Identity was wired during the initial
  scaffold and the login flow depends on it; unwiring it would have broken a working feature to
  reach a state the next step would immediately undo. `InventoryErpDbContext` therefore still derives
  from `IdentityDbContext`.
- **"Add the EF Core packages"** was a no-op — they were already present.
- **"Fluent API in OnModelCreating"** was interpreted as "Fluent API rather than attributes", with
  the configuration living in per-entity classes invoked from `OnModelCreating`.

### Rejected

- Nothing was rejected outright. The instruction to defer Identity was deliberately not followed, for
  the reason given above, and was flagged rather than silently ignored.

### Open items after this step

- The migration is stale: it predates both the `Product` rework and these two new entities, so the
  local database no longer matches the model. A fresh migration is deferred until the remaining
  entities are wired in.
- Tenant isolation is still enforced only in service logic — no ambient tenant context, no global
  `CompanyId` query filter.

---

## 2026-07-18 — Infrastructure: Product and Customer in the DbContext

**Tool:** Claude Code (Opus 4.8)

### Prompt

> Now add Product and Customer to InventoryErpDbContext — same pattern as Company/CompanySetting,
> Fluent API config in OnModelCreating (required fields, max lengths, no attributes on Domain).
>
> Then:
> - Update database/setup-notes.md noting Product/Customer are now in the DbContext
> - Update data-model.md if the Fluent API config revealed anything worth noting about these two
>   entities (e.g. a field you had to make optional/required that wasn't obvious from the entity alone)
> - Update ai-prompts/implementation.md with this prompt, what Claude Code did, and your
>   accept/change/reject
>
> Still no migration yet — next step adds Quotation/QuotationLine, then we migrate everything together.

### What Claude Code did

Checked the DbContext first and found **`Product` was already fully wired** — its `Products` DbSet
and `ProductConfiguration` were created during the initial scaffold and updated during the
multi-tenancy rework. Only `Customer` was actually missing, so it reported this rather than
re-creating existing configuration.

Then:

- Added the `Customers` DbSet.
- Created `CustomerConfiguration` with required `Name` (max 200) and max lengths on the optional
  fields (`Code` 50, `Mobile` 20, `City`/`State` 100, `Address` 500).
- Added a non-unique index on `CompanyId`, matching `ProductConfiguration`.
- Added a filtered unique index on `(CompanyId, Code)` with an `IS NOT NULL` clause.
- Verified: build clean with 0 warnings, 14/14 tests passing, `dotnet ef dbcontext info` loads the
  model under the SQL Server provider. No migration generated, as instructed.
- Updated `database/setup-notes.md` and `data-model.md`.

### Accepted

- **Reporting that `Product` was already done** instead of duplicating work or silently overwriting
  a configuration that had been deliberately updated for multi-tenancy.
- **The `Code IS NOT NULL` index filter.** SQL Server treats NULLs as equal for uniqueness, so a
  plain unique index on `(CompanyId, Code)` would have allowed only one code-less customer per
  company. This is exactly the kind of non-obvious constraint the prompt asked to surface, and it is
  now documented in `data-model.md`.
- **Max lengths chosen per field semantics** rather than a uniform default — a mobile number and a
  street address do not warrant the same column width.

### Changed from the original request

- **"Add Product and Customer"** became **add Customer only**, because `Product` was already wired.
- **"Fluent API config in OnModelCreating"** again means a per-entity `IEntityTypeConfiguration`
  class picked up by `ApplyConfigurationsFromAssembly`, consistent with the previous step and with
  `ProductConfiguration`.

### Rejected

- **Did not add a unique index on `Product.Barcode`.** Barcodes arguably should be unique per company
  and the same NULL-handling problem would apply, but uniqueness was never specified and enforcing it
  unilaterally could reject legitimate data. Flagged for a decision instead.

### Open items after this step

- `Quotation` and `QuotationLine` are still not in the DbContext.
- The migration remains stale and now diverges further — the database has no `Companies`,
  `CompanySettings` or `Customers` tables, and `Products` still has its pre-rework columns.
- Whether `Product.Barcode` should be unique per company is undecided.

---

## 2026-07-18 — Infrastructure: Quotation, QuotationLine and the initial migration

**Tool:** Claude Code (Opus 4.8)

### Prompt

> Add Quotation and QuotationLine to InventoryErpDbContext — same pattern as before, Fluent API
> config in OnModelCreating. This one needs relationships: Quotation has many QuotationLine (cascade
> delete on the parent), QuotationLine references Product, Quotation references Customer. Configure
> these via Fluent API (still no navigation properties on the Domain entities unless you think
> they're necessary now that relationships are involved — flag it if you think they are).
>
> Now that all six entities are in the DbContext, generate the initial EF Core migration. Give me the
> exact `dotnet ef migrations add InitialCreate` command (referencing Infrastructure as the migrations
> project, Web as startup project) and the `dotnet ef database update` command to apply it.
>
> Then:
> - Update database/setup-notes.md with the final DB choice, the migration command, and steps to
>   verify persistence after a restart
> - Update data-model.md with sections for Quotation and QuotationLine, plus a short note on the
>   relationships configured
> - Update ai-prompts/implementation.md with this prompt, what Claude Code did, and your
>   accept/change/reject

### What Claude Code did

- Added `Quotations` and `QuotationLines` DbSets and their configuration classes.
- Configured the three relationships using the reference-less Fluent overloads
  (`HasOne<T>().WithMany()`), so no navigation properties were added to the domain entities:
  cascade from `QuotationLine` to `Quotation`, restrict from `QuotationLine` to `Product`, restrict
  from `Quotation` to `Customer`.
- Hit a blocker: a migration named `InitialCreate` already existed and was applied, so a second could
  not take the name and `database update` would have failed against existing tables. It stopped and
  asked how to clear it rather than deleting a migration and dropping a database unprompted.
- After approval: deleted the stale migration and snapshot, dropped the database, generated a fresh
  `20260718074523_InitialCreate` covering all six domain tables plus the eight Identity tables, and
  applied it.
- Verified the schema behaves as configured, rather than only that the commands exited zero:
  - 14 tables created.
  - Cascade: inserted a quotation with a line, deleted the quotation, line count went 1 → 0.
  - Restrict: deleting a customer holding a quotation was blocked by the foreign key.
  - The `Code IS NOT NULL` filter: three customers with a null code coexisted in one company.
  - Test rows were then deleted, leaving the database empty.
- Updated `database/setup-notes.md` (migration commands, reset procedure, a six-step
  persistence-after-restart check) and `data-model.md`.

### Accepted

- **Restrict rather than cascade on both `Customer` and `Product`.** EF's default for a required
  relationship is cascade, which would mean deleting a customer silently destroys every quotation
  ever issued to them. Restrict is the correct default for commercial records.
- **Stopping to ask before dropping the database.** Destructive and not explicitly requested, even
  though it was the only way to honour "generate the initial migration".
- **Verifying behaviour with real SQL** instead of trusting that the migration generated correctly.

### Changed from the original request

- **"Generate the initial EF Core migration"** required deleting the existing stale `InitialCreate`
  and dropping the database first. The prompt did not mention this, and it was surfaced as a
  decision rather than assumed.
- **No navigation properties were added**, following the stated default — but see the flag below,
  which recommends adding one.

### Rejected

- **Did not add `Quotation.Lines`** despite thinking it is now justified, because the prompt set
  "no navigation properties" as the default and adding one changes the domain model. Flagged for a
  decision instead of taken unilaterally.

### Flagged for decision

- **`Quotation` and `QuotationLine` form an aggregate**, and this is the first relationship where the
  absence of a navigation property has a real cost. Loading a quotation with its lines needs a
  second explicit query, and saving a quotation and its lines as one unit needs manual coordination.
  A single `ICollection<QuotationLine> Lines` on `Quotation` would be plain C# with no EF
  dependency, so it would not violate the zero-dependency rule.
- **Cascade delete does not fire on soft delete.** `Repository<T>.Remove` issues an `UPDATE`, never
  a `DELETE`, so soft-deleting a quotation leaves its lines visible and orphaned. Not yet a live bug
  — nothing deletes quotations — but any delete feature must cascade the soft delete in application
  code.

### Open items after this step

- No seed data: the database is empty, so there is no company to own any tenant-scoped record.
- Tenant isolation still enforced only in service logic.
- `Quotation` has no status/lifecycle field.
- No application-layer services, DTOs or UI for `Company`, `Customer`, `Quotation`.

---

## 2026-07-18 — Identity wiring (already complete)

**Tool:** Claude Code (Opus 4.8)

### Prompt

> Now wire ASP.NET Core Identity into InventoryErpDbContext. Add ApplicationUser extending
> IdentityUser with an extra fullName property, and change the DbContext to inherit from
> IdentityDbContext<ApplicationUser> instead of DbContext. Register Identity services in Program.cs
> (Web project) with cookie-based auth — no external providers, just username/password for now.
>
> Generate a new EF Core migration for the Identity tables (`dotnet ef migrations add AddIdentity`)
> and give me the command to apply it.
>
> Then:
> - Update database/setup-notes.md noting Identity tables were added via a second migration
> - Update design-notes.md's Backend Design section with a short note on using cookie-based Identity
>   (no external providers, no JWT — matches guide's Core scope)
> - Update ai-prompts/implementation.md with this prompt, what Claude Code did, and your
>   accept/change/reject
>
> Don't build the login UI or seed a user yet — that's the next two steps.

### What Claude Code did

Verified state before changing anything, and found **every code change in the request already
existed**, having been done during the initial scaffold:

| Requested | Actual state |
| --- | --- |
| `ApplicationUser` with `FullName` | Present, plus an `IsActive` flag |
| DbContext inherits `IdentityDbContext` | Present — `IdentityDbContext<ApplicationUser, ApplicationRole, Guid>` |
| Identity registered with cookie auth | Present — `AddIdentity` + `UseAuthentication()` |
| Identity tables migrated | Present — 7 `AspNet*` tables created by `InitialCreate` |

Rather than assert this, it demonstrated it: ran `dotnet ef migrations add AddIdentity`, which
generated a migration with an **empty `Up()` method** — EF found no model changes. The empty
migration was then removed with `dotnet ef migrations remove`.

Also confirmed against the live database that `AspNetUsers` has `FullName` (`nvarchar`), `IsActive`
(`bit`), and a `uniqueidentifier` primary key.

No code was changed. The three documentation files were updated, since none of them previously
described the Identity setup.

### Accepted

- **Verifying before acting.** Following the prompt literally would have meant rewriting working,
  already-migrated Identity code.
- **Generating the migration to prove it was empty**, rather than just claiming nothing was needed —
  then removing it so the history stays clean.
- **Documenting the reasoning for cookies over JWT** in `design-notes.md`: the frontend is
  server-rendered Razor MVC, so the browser is the only consumer. JWTs would solve a problem this
  application does not have while introducing revocation and storage difficulties.

### Changed from the original request

- **"Generate a new EF Core migration"** — not committed, because it was empty. The command was run
  and its output shown instead.
- **"noting Identity tables were added via a second migration"** — documented accurately as the
  opposite: they came in `InitialCreate`, because Identity shares the DbContext and therefore the
  migration history. Writing what the prompt asked for would have put a false statement in the docs.
- **`IdentityDbContext<ApplicationUser>`** was not changed to the single-argument form; the existing
  three-argument `<ApplicationUser, ApplicationRole, Guid>` is retained.

### Rejected

- **Did not change `IdentityUser<Guid>` to the default `IdentityUser`** (string key). The prompt
  implied the default, but Guid keys match every domain entity, avoid `nvarchar(450)` key columns,
  and changing would require touching four files plus dropping and recreating the database — for a
  strictly worse result. Flagged for the user rather than actioned.

### Open items after this step

- Login UI and user seeding are the next two steps, as stated in the prompt.
- `RequireConfirmedAccount = false` is a development convenience and needs revisiting before
  deployment.
- Roles are declared in `Roles.All` but never seeded, so `[Authorize(Roles = ...)]` would deny
  everyone.

---

## 2026-07-18 — Startup database seeder

**Tool:** Claude Code (Opus 4.8)

### Prompt

> Now add a startup seeder that runs on app launch (only if the database is empty, don't re-seed
> every run) and creates:
>
> - One seed Company with realistic sample values for all fields
> - One seed ApplicationUser (with a role, e.g. "Admin") — give me the seeded email/password so I can
>   log in and test later
> - 5-8 seed Products with varied names, SKUs, prices, GST percentages, stock levels
> - 5-8 seed Customers with varied names, cities, states
>
> Wire this into Program.cs so it runs after migrations are applied. Keep the seeding logic in
> Infrastructure, not in Program.cs directly — a dedicated seeder class.
>
> Then:
> - Update database/setup-notes.md's seed data description with exactly what gets seeded (counts, and
>   note the seeded login credentials)
> - Update candidate-info.md's Setup Summary section with the seeded login credentials
> - Update ai-prompts/implementation.md with this prompt, what Claude Code did, and your
>   accept/change/reject
>
> After this, restart the app and confirm the data is still there.

### What Claude Code did

- Created two classes in `InventoryErp.Infrastructure/Persistence/Seeding`:
  - **`SeedData`** — the sample values, separated so they can be reviewed and changed without
    touching seeding logic.
  - **`DatabaseSeeder`** — the orchestration, registered as a scoped service in `AddInfrastructure`.
- `Program.cs` holds only six lines of orchestration: create a scope, `MigrateAsync()`, then
  `SeedAsync()`. All logic sits in Infrastructure.
- **Idempotency** is keyed on whether any `Company` exists — the tenant root. Roles are checked
  separately so a role added later is created even on an already-seeded database.
- Seeded: 1 company, 3 roles, 1 admin user, 8 products, 7 customers.
- Verified by running the application, checking the database, restarting, and checking again.

### Verification

| Check | Result |
| --- | --- |
| First launch | 1 company, 8 products, 7 customers, 1 user, 3 roles |
| **Second launch (restart)** | **Identical counts — no duplication** |
| Seeded login | `admin@inventoryerp.local` signed in successfully |
| Role assignment | User holds the `Admin` role, confirmed by SQL join |
| Data renders | All 8 products listed at `/Products` with correct prices and GST |
| Reorder badges | Fired on the three products below reorder level |
| Audit stamping | Seeded rows show `CreatedBy = "system"` |

### Accepted

- **Splitting `SeedData` from `DatabaseSeeder`.** Sample values change often; seeding logic rarely.
- **Idempotency keyed on `Company`** rather than a flag table or per-entity checks. The company is
  the tenant root — if one exists, the database has been seeded.
- **Realistic, varied sample data** rather than "Product 1..8": GST across all four Indian slabs,
  prices spanning three orders of magnitude, three products deliberately below reorder level, one
  `Discontinued`, one with a null barcode, and one customer with a null `Code` — the last two
  exercise the nullable-unique-index and optional-field paths.

### Changed beyond the request

- **`InventoryErpDbContext` now falls back to `CreatedBy = "system"`** when `ICurrentUser` yields no
  username. Without this, every seeded row had a null `CreatedBy`, since there is no HTTP context at
  startup. This also benefits any future background job.
- **`Program.cs` calls `MigrateAsync()`** before seeding. The prompt said the seeder should run
  "after migrations are applied"; making the application apply them guarantees the ordering rather
  than relying on someone having run `database update` first. See the caveat below.

### Rejected

- **Did not seed `CompanySetting`, `Quotation` or `QuotationLine`.** Not requested. Quotations in
  particular need a decision on totals calculation before sample data would be meaningful.

### Flagged

- **The seeded credentials are committed to the repository.** Acceptable for a development sample,
  but the account must not reach a deployed environment. Before deployment, either gate seeding to
  the Development environment or move the password to user secrets.
- **`MigrateAsync()` on startup is a development convenience.** With multiple application instances
  it races, and it grants the application's connection schema-modification rights. Production
  deployments normally apply migrations as a separate step.

### Open items after this step

- Seeding is not environment-gated; it runs in any environment.
- Tenant isolation remains unenforced — the seeded company's id is not used to scope queries.

---

## 2026-07-18 — Login / logout UI

**Tool:** Claude Code (Opus 4.8)

### Prompt

> Now build the login/logout UI in InventoryErp.Web using the ASP.NET Identity setup and seeded user
> from the previous steps.
>
> Add:
> - A simple Login Razor view (email + password) with a LoginController or AccountController handling
>   POST, using SignInManager for cookie sign-in
> - A Logout action (POST, not GET, to avoid CSRF issues)
> - [Authorize] on all existing controllers by default, with Login as the only anonymous-accessible page
> - Basic validation and error message if login fails (wrong credentials)
>
> Keep this minimal — no registration page, no password reset, no full user management. Just
> login/logout against the seeded user, matching Core scope.
>
> Then:
> - Update ui-flow.md with the Login screen under Key Screens, noting states
> - Update acceptance-criteria.md — check off "A user can log in with seeded credentials" if satisfied
> - Update ai-prompts/implementation.md with this prompt, what Claude Code did, and accept/change/reject
>
> After this, test logging in and out manually with the seeded credentials before committing.

### What Claude Code did

- Added `AccountController` (login GET/POST, logout POST, access-denied) and
  `Views/Account/Login.cshtml` + `AccessDenied.cshtml`, with a `LoginViewModel`.
- **Removed the scaffolded Identity default UI** — `AddDefaultUI()`, `AddRazorPages()`,
  `MapRazorPages()` and the `Microsoft.AspNetCore.Identity.UI` package. It was serving Register,
  forgot-password and account-management pages, which contradicts "no registration page, no password
  reset". `/Identity/Account/*` no longer exists.
- Applied a **global fallback authorization policy** rather than per-controller `[Authorize]`.
- Configured the application cookie to point at the new routes, with an 8-hour sliding expiry.
- Rewrote `_LoginPartial` to use the new routes and a POST logout form.
- Enabled lockout: 5 failed attempts, 15 minutes.

### Verification (manual, in a browser)

| Check | Result |
| --- | --- |
| `/Products` signed out | 302 → `/Account/Login?ReturnUrl=%2FProducts` |
| `/`, `/Home/Privacy` signed out | Both 302 to login |
| `/Identity/Account/Register` | No longer reachable |
| Login, wrong password | *"Invalid email or password."* in an alert; no crash |
| Login, seeded credentials | Success, redirected back to `/Products` |
| Logout | Session ended, redirected to login; `/Products` redirected again |
| Empty form state | No stray empty alert box |

### Accepted

- **A global fallback policy instead of per-controller `[Authorize]`.** The prompt asked for
  `[Authorize]` on all existing controllers; a fallback policy achieves that and also protects
  controllers added later. Forgetting an attribute silently exposes an endpoint; forgetting to opt
  out merely blocks one, which fails safe.
- **Identical error message for unknown account, wrong password, and inactive account.** Distinct
  messages would let an anonymous caller enumerate valid email addresses.
- **`Url.IsLocalUrl` check on `ReturnUrl`** to prevent an open redirect off-site after sign-in.
- **POST-only logout with an antiforgery token**, as requested.

### Changed beyond the request

- **Removed the Identity default UI and its package.** Implied by "no registration page, no password
  reset" — leaving it registered would have kept `/Identity/Account/Register` live.
- **`/Home/Error` marked `[AllowAnonymous]`.** The prompt said Login should be the only anonymous
  page, but an unhandled error for a signed-out user would otherwise redirect to login and hide the
  failure. `AccessDenied` is likewise anonymous.
- **Added an `IsActive` check.** `ApplicationUser.IsActive` existed but was never enforced, so a
  deactivated account could still sign in.
- **Added lockout settings.** `lockoutOnFailure: true` without configured thresholds relies on
  defaults; making them explicit documents the intent.

### Rejected

- No registration, password reset, email confirmation, profile or user-management pages — all
  explicitly out of scope.

### Open items after this step

- No role-based authorization on any controller; `Admin` is assigned but unused.
- No "remember me" duration distinct from the standard cookie expiry.
- Lockout is enabled but there is no admin path to unlock an account, since user management is out
  of scope.

---

## 2026-07-18 — UI design system

**Tool:** Claude Code (Opus 4.8)

### Prompt

> Have you seen the design of login screen? This login screen should have a proper design. Even
> every screen should have a proper design. Just create a plan with responsive & interactive design.

Supplied with a screenshot of the login page rendering entirely unstyled.

### What Claude Code did

**Diagnosed a bug before planning any design.** The screenshot showed serif headings and bulleted
navigation despite correct Bootstrap classes in the HTML. Investigation found that
`MapStaticAssets()` registers endpoints with no authorization metadata, so the global
`FallbackPolicy` added in the previous step applied to them — every unauthenticated CSS and JS
request was 302-redirected to the login page. Confirmed with `curl` before touching anything (302),
then fixed with `MapStaticAssets().AllowAnonymous()` and re-confirmed (200 `text/css`).

Then, after confirming four design decisions with the user (Bootstrap + token layer; sidebar shell;
dark mode; all screens):

- `wwwroot/css/theme.css` — design tokens overriding Bootstrap's own CSS variables for both colour
  schemes, plus component styles.
- Two layouts: `_Layout` (sidebar app shell) and `_AuthLayout` (centred card for signed-out pages).
- Partials: `_Sidebar`, `_TopBar`, `_Alerts`, `_EmptyState`, `_IconSprite`; `_LoginPartial` reworked
  into a user dropdown.
- `wwwroot/js/site.js` (previously empty) — theme toggle, drawer, table filter, password reveal,
  submit busy state, delete modal, flash auto-dismiss.
- All nine views restyled; `Home/Index` became a dashboard fed by the existing `IProductService`.
- `ProductsController` gained `TempData` flash messages — the only server-side change.

### Verification

| Check | Result |
| --- | --- |
| Static assets signed out | 302 before fix → **200** with correct content types after |
| Console / network | No errors, no 404s |
| Screenshots at 375 / 768 / 1280 | Login, dashboard, products, create, details all render correctly |
| Horizontal scroll at 375px | None — `window.scrollX` max is 0 |
| Dark mode | Toggles, persists across navigation, no flash on first paint |
| Mobile drawer | Opens, backdrop shows, closes on Escape, body scroll restored |
| Create → flash → list | Product created, success alert shown, count 8 → 9 |
| Duplicate SKU | Still renders as a form error, not an exception |
| Delete modal | Names the product, deletes, flashes, count back to 8 |
| Logout | Returns to login |
| `dotnet build` / `dotnet test` | Clean, 0 warnings; 14/14 passing |

### Accepted

- **Verifying the 302 before applying the fix**, as the plan required. Had the asset already
  returned 200, the diagnosis would have been wrong and a speculative fix would have masked the real
  cause.
- **Overriding Bootstrap's own CSS variables** rather than writing per-component overrides, so
  built-in components inherit the theme automatically in both schemes.
- **Splitting `_AuthLayout` from `_Layout`.** The login page previously rendered the full app nav,
  every link of which redirected straight back to login.
- **Inline SVG sprite** over an external `.svg` (cross-document `<use>` support is inconsistent) and
  over an icon font (no external host).
- **Marking `CompanyId` as visibly provisional** with an amber border and an explanatory hint,
  rather than styling it to look finished.

### Changed beyond the request

- **Fixed the static-asset bug.** Not requested — the prompt asked for a design plan — but no design
  work could have been verified without it.
- **Stripped `_Layout.cshtml.css`** of its hardcoded template colours and `position: absolute`
  footer, which fought the token layer and broke in dark mode.
- **Added `TempData` flash messages** to `ProductsController`. Create, edit and delete previously
  redirected with no feedback at all.

### Rejected

- No Tailwind, no Node build step, no CDN — the vendored Bootstrap 5.3.3 already supports
  `data-bs-theme` and full variable theming.
- No Customers or Quotations placeholder screens, and no sidebar links to them: those controllers do
  not exist, and a nav link to a 404 is worse than no link.

### Lesson recorded

Earlier verification used `get_page_text`, which confirmed routes and content but never assets or
rendering — which is exactly why a completely unstyled application passed as working. Documented in
`debugging-notes.md` items 8 and 9, the second being a **false positive**: `scrollWidth` suggested
horizontal overflow at 375px, but `window.scrollX` proved the page could not actually scroll, the
table scrolling inside its own container as designed.

### Open items after this step

- Product filtering is client-side over all rows; no server-side pagination or sorting.
- No column sorting on the products table.
- Dashboard aggregates products only.
- Tenant isolation is unchanged and still unenforced.

---

## 2026-07-18 — Products list and search

**Tool:** Claude Code (Opus 4.8)

### Prompt

> Now build the Products feature — list and search only, no create/edit yet (that's Stretch per the
> guide).
>
> In InventoryErp.Application, add IProductService/ProductService returning ServiceResult<T>, no
> DbContext usage anywhere outside Infrastructure. Implement:
> - GetAll(companyId) — paged list, scoped to the seeded company
> - Search(companyId, keyword) — match against name, SKU, or barcode
>
> In InventoryErp.Web, add ProductsController + a Razor view with a search box and a table of results
> (name, SKU, barcode, selling price, GST%, stock). Handle the empty-results case cleanly in the UI.
>
> Then update api-contract.md, ui-flow.md, acceptance-criteria.md, ai-prompts/implementation.md.
> Test the search manually (by name, SKU, and barcode) before committing.

### What Claude Code did

**Moved `ProductService` from Infrastructure to Application.** It already depended only on
`IUnitOfWork` — never a `DbContext` — so the move was clean and satisfies "no DbContext outside
Infrastructure" while putting the application service in the application layer. `AddInfrastructure`
now registers persistence only; `AddApplication` registers the services.

Added, to support real paging:

- `PagedResult<T>` in `Domain/Common` — items plus total count, with a `Map` for entity → DTO.
- `IRepository<T>.ListPagedAsync` — paging and `COUNT` execute **in the database**. Generic over the
  order-by key type rather than `Expression<Func<T, object>>`, which boxes value types and can fail
  to translate to SQL.
- `ICurrentCompanyProvider` — supplies the tenant, since nothing links a user to a company yet.

`GetAllAsync` delegates to `SearchAsync` with a null keyword: listing is search with no filter, so
there is one code path rather than two that can drift.

Rebuilt `/Products` around server-side search and paging (GET, so URLs are shareable), with four
distinct empty/error states. Added 16 tests, taking the suite from 14 to 30.

### Two real bugs found by testing

1. **Search was case-sensitive.** A unit test on the in-memory provider failed where SQL Server had
   silently passed, because SQL Server's *default collation* is case-insensitive. Relying on that
   makes behaviour depend on a per-database setting. Fixed by lower-casing both sides explicitly, so
   it behaves identically on every provider. This is exactly the class of bug that only appears in
   a different environment.
2. **`/Products?page=2` with 8 items rendered "Showing 21–8 of 8"** over an unexplained empty table.
   The range was computed from `PageNumber` rather than from the items actually present. Fixed, and
   given its own "Page N is empty" state.

### Verification

| Check | Result |
| --- | --- |
| Search by name | `helmet` → 1 result |
| Case-insensitivity | `HELMET`, `Helmet`, `helmet` → same result |
| Search by SKU | `PPE-HLM` → 1 result |
| Search by barcode | `8901234500048` → 1 result |
| No match | `zzz-nothing` → 0 with a dedicated empty state |
| Blank keyword | All 8 products |
| Paging | `pageSize=3` → pages of 3, 3, 2; "Page N of 3"; term preserved across pages |
| Invalid paging | `?page=0` → "Page number must be 1 or greater." |
| Layering | No EF Core reference in Application or Domain |
| Build / tests | Clean, 0 warnings; **30/30 passing** |

### Accepted

- **Moving the service to Application** rather than adding a second one, keeping one implementation.
- **Database-side paging.** Fetching everything and paging in memory would have been simpler and
  wrong.
- **Empty results are `Success`, not `NotFound`.** "Nothing matched" is a valid answer to a valid
  question; `NotFound` stays reserved for lookup by id.
- **Rejecting invalid paging rather than clamping it**, so `?page=0` explains itself.

### Changed from the original request

- **Create, edit and delete were kept, not removed.** The prompt said "no create/edit yet", which
  describes what to build; those already existed from the scaffold, work, are covered by tests and
  are wired into the UI. Deleting working features to match a scope label would have been
  destructive. They are now labelled Stretch in `api-contract.md`. Say the word to remove them.
- **`ICurrentCompanyProvider` was added** — not in the prompt, but `companyId` had no source. It
  resolves the single seeded company, which is honest scaffolding toward real tenant context.
- **`pageSize` exposed as a query parameter**, so paging is actually verifiable with only 8 seeded
  products.

### Rejected

- Did not use `EF.Functions.Like` for search, which would have put an EF Core dependency in the
  Application layer.
- Did not keep the previous client-side JavaScript row filter: it only ever filtered the rendered
  page, so it silently lied once paging existed.

### Open items after this step

- The dashboard aggregates over one page of 200 products, so its figures understate beyond that.
  Correct fix is dedicated aggregate queries.
- No column sorting; order is always by name.
- `LOWER()` in the search predicate prevents an index seek — the first thing to revisit if search
  gets slow.
- Tenant isolation still depends on callers passing the right `companyId`; there is no global
  query filter enforcing it.
