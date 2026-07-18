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

---

## 2026-07-18 — Customers list

**Tool:** Claude Code (Opus 4.8)

### Prompt

> Now build the Customers feature — list only, no create/edit (Stretch scope per the guide).
>
> In InventoryErp.Application, add ICustomerService/CustomerService returning ServiceResult<T>, no
> DbContext usage outside Infrastructure. Implement GetAll(companyId) — scoped to the seeded company,
> paged if it makes sense with the seed data size.
>
> In InventoryErp.Web, add CustomersController + a Razor view with a table (name, code, mobile, city,
> state). Handle the empty-results case cleanly.
>
> Then update api-contract.md, ui-flow.md, acceptance-criteria.md, ai-prompts/implementation.md.
> Test manually before committing.

### What Claude Code did

Followed the pattern established by Products, reusing everything rather than re-inventing it:

- `CustomerDto`, `ICustomerService`, `CustomerService` in Application, registered in
  `AddApplication`. Depends only on `IUnitOfWork`.
- Reused `PagedResult<T>` and `IRepository<T>.ListPagedAsync` from the Products step — paging and
  `COUNT` run in the database; no new infrastructure was needed.
- Reused `ICurrentCompanyProvider` for tenant scoping.
- `CustomersController` + `Views/Customers/Index.cshtml`, mirroring the Products view's state
  handling.
- **Added the Customers link to the sidebar** under a new "Sales" group. It had been deliberately
  omitted while no controller existed; now one does.
- Added an `i-users` icon to the sprite (only `i-user`, singular, existed).
- 11 new tests, taking the suite from 30 to 41.

### Verification

| Check | Result |
| --- | --- |
| Listing | All 7 seeded customers, ordered by name, "Showing 1–7 of 7" |
| Null `Code` / `Mobile` | "Walk-in / Counter Sales" renders em-dashes |
| Paging | `pageSize=3` → 1–3 of 7, "Page 1 of 3"; page 2 → 4–6 |
| Page past the end | `page=4` → "Page 4 is empty" |
| Invalid paging | `page=0` → "Page number must be 1 or greater." |
| Sidebar | Link present, highlights when active |
| Console errors | None |
| Build / tests | Clean, 0 warnings; **41/41 passing** |

### Accepted

- **Paging included despite only 7 seeded customers.** The prompt said "paged if it makes sense" —
  the infrastructure already existed, so it cost nothing, and a customer list is exactly the kind
  that grows. The pager stays hidden at the default page size of 20, so there is no visible clutter.
- **Read-only means one method.** `ICustomerService` exposes only `GetAllAsync` rather than stubs
  that throw. The interface states the scope.
- **No call-to-action on the empty state.** The Products empty state offers "New product"; this one
  cannot, because there is no create screen. Offering a button that goes nowhere would be worse
  than none.
- **No search box**, unlike Products — it was not in scope, and an input that filters nothing is
  worse than no input.

### Changed beyond the request

- **Sidebar navigation updated.** Not mentioned in the prompt, but leaving the link out would have
  made the feature unreachable except by typing the URL.
- **New `i-users` sprite icon** for the nav item.

### Rejected

- No create, edit or delete — explicitly Stretch scope.
- Did not duplicate the paging helpers for customers; the generic `IRepository<T>` method already
  covers any entity.

### Open items after this step

- No search or filtering on customers.
- No customer detail view — the list shows five fields; `Address` is mapped in the DTO but not
  displayed anywhere.
- Tenant isolation still depends on callers passing the right `companyId`.

---

## 2026-07-18 — Quotations service layer

**Tool:** Claude Code (Opus 4.8)

### Prompt

> Now start the Quotations feature — service layer only for this step, no UI yet.
>
> In InventoryErp.Application, add IQuotationService/QuotationService returning ServiceResult<T>.
> Implement CreateQuotation(companyId, customerId, quotationDate, validUntil, notes, List<line
> items: productId, quantity, unitPrice, discountPercent, gstPercent>)
>
> Server-side logic:
> - Compute each line's taxAmount and totalAmount from quantity, unitPrice, discountPercent, gstPercent
> - Sum lines into the Quotation's subTotal, taxAmount, discountAmount, totalAmount
> - Generate a quotationNumber (your call on format, tell me what you chose and why)
> - Reject and return a failure ServiceResult if: customerId doesn't exist, there are zero line
>   items, any line has quantity <= 0, or any productId doesn't exist
>
> Don't build the controller or Razor view yet. Just the service, its DTOs, and enough for me to
> verify the calculation logic works.
>
> Then update api-contract.md and ai-prompts/implementation.md. Don't update acceptance-criteria.md
> yet — nothing there is satisfied until the UI exists.

### What Claude Code did

- `CreateQuotationRequest` / `CreateQuotationLineRequest`, `QuotationDto` / `QuotationLineDto`,
  `IQuotationService`, `QuotationService` — all in Application, depending only on `IUnitOfWork`.
- **Extracted the arithmetic into `QuotationCalculator`**, a small internal static class with no
  persistence dependency, so the money maths can be tested and reasoned about on its own.
  `InternalsVisibleTo` exposes it to the test project rather than making it public API.
- Extended `IRepository<T>.ListPagedAsync` with a `descending` flag, needed to find the highest
  existing quotation number. Three existing call sites were updated to name the
  `cancellationToken` argument.
- **33 new tests**, taking the suite from 41 to 74: 8 on the calculator alone, 25 on the service.

### Decisions

**GST is charged after discount, not on the gross.** This is the single most consequential choice
here and the prompt did not specify it. Indian GST is levied on the transaction value after any
discount shown on the invoice. On a 1,000 line at 10% off and 18% GST the difference is 162 versus
180 — an 18.00 overstatement per line, every line. There is a test named for this specifically.

**Rounding is half-away-from-zero to 2dp**, applied as each figure is produced. .NET's default is
banker's rounding, which is not what invoices use. Header totals sum already-rounded line figures,
so the stored header always reconciles exactly with the stored lines — no cent-level drift between
a quotation and the sum of its rows.

**Quotation number: `QT-{yyyy}-{NNNN}`**, sequential per company per year. Human-readable and
quotable over the phone; sorts chronologically as text; the year segment keeps sequences short and
resets annually; per-company scoping matches the existing unique index. Derived from the highest
existing number rather than a row count, because counting breaks the moment anything is deleted.

### Verification

| Check | Result |
| --- | --- |
| Build | Clean, 0 warnings |
| Tests | **74/74 passing** |
| Calculation, no discount/tax | 3 × 100 → 300.00 |
| Discount then GST | 10 × 100, 10% off, 18% → gross 1000, disc 100, tax 162, total 1062 |
| GST slabs 5/12/18/28 | All round correctly to 2dp |
| Half-up rounding | 2.50 at 5% → tax 0.13, not 0.12 |
| Header reconciles with lines | Asserted across a 3-line quotation with mixed rates |
| Number sequence | 0001 → 0002 → 0003; resets per year; independent per company |
| Rejections | No lines, qty ≤ 0, negative price, discount outside 0–100, valid-until before date |
| Missing refs | Unknown customer and unknown product both → `NotFound`, nothing written |
| Cross-tenant | Another company's customer/product read as `NotFound` |

Expected values in the tests were checked by hand rather than copied from the implementation —
e.g. 7 × 425.00 at 12.5% and 12%: gross 2975.00, discount 371.875 → 371.88, taxable 2603.12,
tax 312.3744 → 312.37, total 2915.49.

### Accepted

- **Totals are never accepted from the caller.** The request has no monetary total fields at all,
  so a client cannot submit its own figures.
- **`UnitPrice` and `GstPercent` come from the caller, not the product.** A quotation must be
  issuable at a negotiated price and must keep the figures it was issued with.
- **Validation before any write**, and existence checks before any insert, so a rejected request
  leaves the database untouched. A test asserts this.
- **Line-level errors are prefixed `Line N:`** so a future UI can point at the offending row.

### Changed beyond the request

- **Cross-tenant references read as `NotFound`.** The prompt said "customerId doesn't exist"; a
  customer belonging to another company is treated as not existing rather than usable, otherwise
  one tenant could quote another's catalogue by guessing an id.
- **Extra validation not asked for:** negative unit price, discount outside 0–100, negative GST,
  and `ValidUntil` earlier than `QuotationDate`. Each would otherwise produce a silently wrong
  document.
- **`descending` added to `ListPagedAsync`** — needed to read the highest existing number.

### Rejected

- No controller, view or `acceptance-criteria.md` update — explicitly out of scope for this step.
- Did not add a `QuotationStatus` lifecycle. Still flagged as missing, but inventing one now would
  pre-empt a decision that has not been made.

### Open items after this step

- **Number allocation has a race.** Read-then-write is not atomic; the unique index is the real
  guarantee and the service retries five times before returning `Conflict`. A database sequence is
  the proper fix if creation becomes concurrent.
- **Numbers can be reused after a soft delete**, since the unique index excludes deleted rows.
- No read, update or delete methods on the service — creation only.
- Quotations have no status/lifecycle field.
- Creating a quotation does **not** decrement product stock; whether it should is undecided.

---

## 2026-07-18 — Quotations UI

**Tool:** Claude Code (Opus 4.8)

### Prompt

> Now add the UI for creating a quotation, using the QuotationService from the last step.
>
> In InventoryErp.Web, add QuotationsController with a Create action (GET shows the form, POST
> submits) and a Razor view: customer dropdown, and a dynamic line-item table (add/remove rows
> client-side) with product dropdown, quantity, unit price, discount%, GST%. On submit, call
> CreateQuotation and show the ServiceResult's errors inline if it fails (e.g. zero quantity, no
> customer selected) — don't just show a generic error page.
>
> Also add the List action + view (table of quotations: number, customer, date, total) and a Details
> action + view (full quotation with line items) — no PDF yet, that's the next step.
>
> Then update ui-flow.md, acceptance-criteria.md, ai-prompts/implementation.md.
> Test manually: create a quotation with 2+ line items, try submitting one with zero quantity and
> confirm it's rejected with a clear message, before committing.

### What Claude Code did

**Extended the service first.** List and detail need data the service did not expose, so
`GetAllAsync` and `GetByIdAsync` were added, along with `CustomerName` / `ProductName` /
`ProductSku` on the DTOs and a `QuotationListItemDto`. Because the entities have no navigation
properties, names are resolved with one batched lookup per page rather than per row.

Also **refactored create**: product existence was checked with one `AnyAsync` per line; it now
fetches all referenced products in a single query and reuses them for both validation and naming.

**Web layer:** `QuotationsController` (Index / Details / Create GET+POST), three Razor views, and
a `QuotationCreateViewModel` separate from `CreateQuotationRequest` — MVC binding needs a mutable
`List<T>` and the view needs dropdown options.

**Dynamic line editor** in `site.js`: clones a `<template>`, renumbers field names to a contiguous
`Lines[0..n]` after every add or remove, prefills price and GST from the selected product without
overwriting non-zero values, and shows a live total preview that mirrors the server calculation.

**Inline error mapping:** `Line N:` prefixed errors are parsed and attached to that row's specific
field, matched on message text. Unrecognised errors fall back to the summary so none is dropped.

11 new tests, taking the suite from 74 to **85**.

### A real UX bug found while testing

Submitting with no customer produced **"The value '' is invalid."** — the framework's model-binding
message, not the `[Required]` message. A non-nullable `Guid` fails binding before validation runs.
Fixed by making `CustomerId` and `Lines[i].ProductId` nullable `Guid?`, after which the intended
"Select a customer." appears. This was precisely the generic message the prompt asked to avoid, and
it would have shipped unnoticed without posting the empty case directly.

### Verification

| Check | Result |
| --- | --- |
| Two-line quotation via browser | `QT-2026-0001` created, redirected to detail with a flash |
| Server totals | subtotal 1990.00, discount 24.50, GST 126.94, total 2092.44 — confirmed by SQL |
| Client preview vs server | Identical (2092.44) |
| Add / remove rows | Renumbered to contiguous `Lines[0..n]` |
| Product prefill | Price and GST filled; a manually set price is not overwritten |
| **Zero quantity** | Rejected — "Quantity must be greater than zero." shown **inline on that row** |
| No customer | "Select a customer." |
| Zero lines | "A quotation must have at least one line." |
| 150% discount | "Discount percent must be between 0 and 100." |
| Form state after failure | Customer, dates, notes and all lines preserved |
| Nothing written on failure | Database stayed at 1 quotation / 2 lines throughout |
| List and detail | Both render correctly; cross-tenant detail returns 404 |
| Console errors | None |
| Build / tests | Clean, 0 warnings; **85/85 passing** |

### Accepted

- **Totals are never posted.** The form submits only quantities, prices and percentages; every
  monetary figure is computed server-side. The preview is explicitly labelled as such.
- **Line-level errors land on the offending input**, not just a summary — the prompt asked for
  inline errors and a summary alone would not have satisfied it.
- **Failed posts preserve everything the user typed.** Losing a ten-line quotation to one bad
  quantity would be worse than the original error.
- **Renumbering on every add/remove**, so removing row 2 of 3 does not leave `Lines[0], Lines[2]`
  and silently drop a line at the model binder.

### Changed beyond the request

- **Added `GetAllAsync` / `GetByIdAsync` to the service** — the prompt asked for List and Details
  actions, which cannot exist without them.
- **Batched name lookups** rather than per-row queries.
- **Sidebar link and an `i-file` icon**, otherwise the feature is reachable only by typing the URL.
- **Nullable `Guid?` on the form model**, for the binding reason above.

### Rejected

- No PDF action — explicitly next step.
- No edit or delete for quotations; not requested, and an issued quotation arguably should not be
  silently editable.
- Did not make the client preview authoritative: it is a convenience, and the server recalculates
  regardless.

### Open items after this step

- Quotations still have no status/lifecycle (draft / sent / accepted / expired). The detail view
  infers "Expired" from `ValidUntil` alone.
- No edit, delete or duplicate action.
- The number-allocation race from the previous step is unchanged.
- Creating a quotation still does not affect stock.

---

## 2026-07-18 — Settings (storage and editing)

**Tool:** Claude Code (Opus 4.8)

### Prompt

> Now start the Settings feature — this step is just storing and editing settings, PDF generation
> comes next.
>
> In InventoryErp.Application, add ISettingsService/SettingsService backed by CompanySetting
> (key/value), returning ServiceResult<T>. Implement:
> - GetSettings(companyId) — returns a strongly-typed CompanySettingsDto (address fields, GSTIN, PAN,
>   invoice terms, invoice footer, primary accent color), reading from the key/value rows and falling
>   back to sensible defaults if a key is missing
> - UpdateSettings(companyId, dto) — upserts the key/value rows
>
> In InventoryErp.Web, add SettingsController + a Razor edit form for these fields, pre-filled from
> GetSettings.
>
> Then update api-contract.md, design-notes.md's Backend Design section, ai-prompts/implementation.md.
> Test manually: change a setting, reload the page, confirm it persisted. Don't touch PDF generation yet.

### What Claude Code did

- Extended `SettingKeys` (Shared) with the new keys: city, state, country, PIN, GSTIN, PAN, invoice
  terms, invoice footer, accent colour.
- `CompanySettingsDto`, `ISettingsService`, `SettingsService` in Application, depending only on
  `IUnitOfWork`.
- **Three-step fallback** per field: stored row → `Company` column → hard default. A blank stored
  value is treated as absent and falls through.
- **Upsert** that skips unchanged values, so audit columns record only real edits.
- `SettingsController` + a Razor form grouped into Registered address / Tax identifiers / Document
  text / Branding, with a colour swatch bound to the hex text input.
- POST-redirect-GET, so a refresh cannot resubmit and the page re-reads from the database — which is
  what actually proves persistence.
- 18 new tests, taking the suite from 85 to **106**.

### The duplication this surfaced

`Address`, `City`, `State`, `Country`, `PinCode`, `GstNumber` and `PanNumber` already exist as typed
columns on `Company`. The prompt asked for the same fields as key/value settings. This is the exact
collision flagged when `Company` was first added — *"company branding now has two possible homes…
settle it before the PDF work, or they will drift."*

Resolved with a documented precedence rather than a merge: **setting wins, column is the fallback.**
An unconfigured company shows its real details on first visit; once saved, settings are
authoritative. Writes do **not** update the `Company` columns, so the two can diverge — recorded in
`design-notes.md` as needing a decision before anything else reads those columns.

### A real bug found by testing

`UpdateSettingsAsync` threw *"another instance with the same key value is already being tracked"* on
a **second** save within one scope. `ListPagedAsync` reads with `AsNoTracking`, so callers hold
detached copies; `DbSet.Update` then conflicts with an already-tracked instance.

Fixed in `Repository<T>` rather than in the service: `Update` and `Remove` now copy values onto the
tracked instance when one exists. This is a **general** defect that would have affected any service
saving the same entity twice in one request — the settings tests simply exposed it first.

### Verification

| Check | Result |
| --- | --- |
| First load, no rows | Form pre-filled from `Company` columns; `CompanySettings` table empty |
| Change City → Pune, PIN → 411001, terms, footer, colour → `#0F766E` | Saved, "Settings saved." flash |
| **Fresh page load** | All five values persisted |
| Stored rows | 10 rows, one per key, values exactly as entered |
| Second save (City → Nashik) | Still 10 rows — updated, not duplicated |
| Audit | `ModifiedBy` = `admin@inventoryerp.local` |
| Server validation | `Gstin=TOOSHORT` → "GSTIN must be exactly 15 characters."; bad colour → "Use a hex colour such as #4F46E5." |
| Colour swatch | Typing hex syncs the swatch, and vice versa |
| Console errors | None |
| Build / tests | Clean, 0 warnings; **106/106 passing** |

### Accepted

- **Column fallback, not just hard defaults.** The prompt asked for "sensible defaults"; falling back
  to the company's real registered address is more sensible than a placeholder.
- **Blank treated as absent.** An accidentally cleared row falls through to the fallback rather than
  blanking a printed document.
- **Blank GSTIN/PAN allowed.** A company may not be GST-registered; requiring them would be wrong.
- **POST-redirect-GET**, which makes the manual persistence test meaningful — the reloaded page is
  reading from the database, not echoing the submitted form.

### Changed beyond the request

- **Fixed `Repository<T>.Update`** for the tracking conflict above — a general fix, not settings-specific.
- **Validation not asked for:** GSTIN/PAN length, hex colour format, field length caps. These values
  are printed on statutory documents, so a malformed GSTIN is worth catching at entry.
- **Sidebar link and an `i-settings` icon**, otherwise the page is reachable only by URL.
- **Client rules matched to the server.** `[StringLength(15, MinimumLength = 15)]` would have
  rejected a blank GSTIN that the service permits; replaced with a regex allowing blank-or-exact.

### Rejected

- No PDF generation — explicitly next step.
- Did not write settings back to the `Company` columns. That is a real decision about which store
  owns the data, and making it silently as a side effect of this step would be the wrong way to
  settle it.
- Did not add a settings cache. Reads are per request and cheap; caching would add invalidation
  complexity for no measured benefit.

### Open items after this step

- **`Company` columns vs settings can diverge** after the first save. Needs a decision before
  anything reads `Company.Address` for documents.
- `Company.Name`, `Tagline`, `Mobile`, `Email`, `Website` and `LogoPath` are not editable anywhere —
  they were not in the requested field list, but a PDF header will want them.
- No logo upload.
- Settings are read fresh on every request; no caching.

---

## 2026-07-18 — Settings: company fields and logo upload

**Tool:** Claude Code (Opus 4.8)

### Prompt

> Before moving to the next feature, I found a gap: the Settings form is missing Company's own
> fields (name, tagline, mobile, email, website, logoPath) — only address/GSTIN/PAN/terms/footer/color
> are editable, but the PDF needs company name and a logo in the header.
>
> Extend SettingsController/the Settings view to also edit: name, tagline, mobile, email, website.
> Add these to CompanySettingsDto and the GetSettings/UpdateSettings service methods.
>
> For logoPath specifically: add a simple file upload (image file, reasonable size limit e.g. 2MB,
> accept png/jpg), save it to wwwroot/uploads/logos/ with a generated filename, and store the
> relative path in Company.logoPath. Update the PDF header to render this logo if present, skip it
> gracefully if not.
>
> Then update data-model.md, ui-flow.md, ai-prompts/implementation.md.
> Test manually: upload a logo, generate a PDF, confirm it appears in the header

### Blocker raised before starting

**There is no PDF generation.** `IPdfGenerator` is an unimplemented placeholder from the original
scaffold — no library chosen, no implementation, no registration, nothing calling it. The previous
prompt said "PDF generation comes next" and it was not built. So "update the PDF header" had no
header to update, and the stated manual test could not run.

Raised before writing any code. Decision: build settings and logo now, PDF next step, so the
library choice is made deliberately rather than smuggled into this change.

Also raised: the `Company` columns vs settings-rows duplication flagged two steps earlier. Decision:
**write both stores**, reads still prefer the setting.

### What Claude Code did

- Extended `CompanySettingsDto` with `Name`, `Tagline`, `Mobile`, `Email`, `Website`, `LogoPath`,
  and added the matching `SettingKeys`.
- **`UpdateSettingsAsync` now mirrors onto the `Company` columns**, resolving the drift. Blank
  optional values are written as null, matching how the columns are modelled; a blank `Name` never
  nulls the required column.
- `ILogoStorage` in Application with a `LogoUpload` record — the Application layer sees no
  `IFormFile`, no `IWebHostEnvironment` and no `System.IO` paths.
- `LogoStorage` in Web (that is where `wwwroot` is), validating size, extension **and magic bytes**,
  writing a GUID-named file, and deleting the previous one.
- Settings view: a Company card and a Logo card with preview, remove checkbox and file input;
  `enctype="multipart/form-data"` on the form.
- **Gitignored `wwwroot/uploads/`** with a tracked `.gitkeep`, so user uploads never enter source
  control but the directory survives a clone.
- 9 new tests, taking the suite from 106 to **115**.

### Verification

| Check | Result |
| --- | --- |
| New fields pre-filled | Name, tagline, mobile, email, website shown from seeded company |
| Logo upload | Saved as `/uploads/logos/{guid}.png`; file on disk |
| Stored in **both** stores | `CompanySettings` row **and** `Company.LogoPath` — mirroring confirmed |
| Renders after reload | `naturalWidth` 160, `naturalHeight` 60 — genuinely decoded, not just a 200 |
| Served correctly | `200 image/png` |
| Replacing a logo | Old file deleted; exactly one file remains |
| `.pdf` extension | Rejected: "The logo must be a PNG or JPG image." |
| **Text file renamed `.png`** | Rejected: "That file is not a valid PNG or JPG image." |
| 3 MB file | Rejected: "The logo must be 2 MB or smaller." |
| After a rejected upload | Existing logo and stored path unchanged |
| Build / tests | Clean, 0 warnings; **115/115 passing** |

### Accepted

- **Magic-byte checking, not just extension.** Extension and `Content-Type` are both client-supplied.
  A renamed text file passes both and was rejected only by the signature check.
- **GUID filenames.** A client filename could contain traversal segments or overwrite an existing file.
- **Delete the old file only after the save succeeds**, so a failed save never leaves a dangling
  reference.
- **`ILogoStorage` as an abstraction** rather than file I/O in the controller — keeps Application
  free of web types and makes a later move to blob storage an implementation change.

### Changed beyond the request

- **Mirrored writes onto the `Company` columns**, per the decision above. This closes a drift I had
  flagged and could not close unilaterally.
- **Gitignored the uploads directory.** Not mentioned, but without it every uploaded logo would be
  committed as source.
- **Validation for the new fields:** name required, length caps, and a shape-only email check
  (deliberately not RFC-strict, which rejects addresses that work).
- **A "remove logo" checkbox**, since otherwise an uploaded logo could never be cleared.

### Rejected

- **No PDF work**, per the decision above. The manual test's PDF step is deferred, not skipped.
- Did not store the image in the database. A path keeps backups small and lets the web server serve
  the file directly — documented with its trade-off in `data-model.md`.
- Did not add image resizing or dimension limits. A 2 MB cap is enough for now; resizing needs an
  imaging library, which is a dependency decision of its own.

### Open items after this step

- **PDF generation still does not exist** — the logo has no consumer yet.
- Database and filesystem can disagree: a restored backup without `wwwroot/uploads` leaves
  `LogoPath` dangling. The UI degrades to a broken image rather than failing.
- No image dimension or aspect-ratio guidance; a very wide logo may not suit a document header.
- Orphaned files are only cleaned on replacement — a logo removed via the checkbox deletes its file,
  but a row deleted directly in SQL would leave the file behind.

---

## 2026-07-18 — Quotation PDF generation, with logo resilience

**Tool:** Claude Code (Opus 4.8)

### Prompt

> Two follow-ups from the logo upload work before we move to global search:
>
> 1. Update QuotationPdfService to handle a missing logo file gracefully — if Company.LogoPath is set
>    but the file doesn't exist on disk (e.g. after a restored backup), skip the logo in the PDF
>    header instead of throwing. Don't error the whole PDF generation over a missing image.
> 2. Decide on and apply a max-dimension constraint for the logo in the PDF header — your call on the
>    exact values, tell me what you chose and why.
>
> Then update data-model.md's note on LogoPath to also mention the PDF generator's missing-file
> behavior, and update ai-prompts/implementation.md.
>
> Test manually: generate a PDF with a valid logo, then manually delete the file from
> wwwroot/uploads/logos/ and generate again — confirm it degrades cleanly instead of erroring.

### Blocker raised before starting

**`QuotationPdfService` did not exist, and neither did any PDF generation.** The only PDF artefact
was `IPdfGenerator.cs`, an unimplemented placeholder from the original scaffold — no library, no
implementation, no registration, nothing referencing it. This was the third time PDF had come up;
the previous step had explicitly deferred it.

All three parts were therefore unactionable as written: no service to update, no header to
constrain, no PDF to generate for the test. Raised before writing code. Decision: build the PDF now
with both behaviours designed in, using **QuestPDF**.

### Licence check

Flagged before installing: QuestPDF's Community licence is free only for organisations under a
revenue threshold; above it, a paid licence is required. This is the same trap as FluentAssertions
earlier in the project. Accepted knowingly; the declaration is explicit in `AddInfrastructure`, so
the choice is visible rather than buried.

### What Claude Code did

- Installed **QuestPDF 2026.7.1** into Infrastructure.
- `IQuotationPdfService` + `GeneratedDocument` in Application; `QuotationPdfService` in
  Infrastructure — so the rendering library never reaches the Application layer.
- Extended `ILogoStorage` with `TryReadAsync`, returning **null** for a blank path, a path outside
  the logos directory, a missing file, or an IO/permission failure.
- Full A4 document: header with logo and company block, quotation meta, line table, totals, notes,
  terms, and a footer with page numbers. Accent colour comes from settings, falling back to the
  default on a malformed value.
- `Quotations/Pdf/{id}` action plus buttons on the list and detail views.
- 6 new tests, taking the suite from 115 to **121**.

### Decision: max logo dimensions — **180 × 60 pt**

About 63 × 21 mm, at 72 pt per inch.

- A4 portrait with 40 pt margins leaves roughly **515 pt** of content width. 180 pt is about a
  third of that, leaving room for the company name, address and tax IDs beside it.
- 60 pt of height keeps the header shorter than the first rows of the line table, so the document
  still opens on the quotation rather than on branding.
- The image is scaled to **fit inside** the box preserving aspect ratio, and never enlarged beyond
  natural size. A 2000 px wide banner, a square mark and a tall crest all fit without distortion
  or pushing the layout apart.

Constrained at **render** time rather than upload, so the original file stays intact and a future
document layout can pick its own box.

### A real gap found by testing

The test `An_unreadable_logo_does_not_fail_the_document` failed initially. `LogoStorage.TryReadAsync`
catches IO and permission errors, but `QuotationPdfService` was **trusting** that contract — a
storage implementation that threw anything else would have failed the whole document, which is
exactly what the prompt asked to prevent. Added a catch in the PDF service too: a decorative image
must never be able to fail a commercial document.

### Verification — the requested manual test

| Step | Result |
| --- | --- |
| PDF with a valid logo | `200`, `application/pdf`, 54,100 bytes, `%PDF-1.4`, filename `QT-2026-0001.pdf` |
| Embedded images | **1** (`/Subtype /Image`) |
| **Deleted the file from `wwwroot/uploads/logos/`**, DB path left dangling | — |
| Regenerated | `200`, valid PDF, 51,771 bytes |
| Embedded images | **0** — the logo was genuinely omitted, not blank-rendered |
| Server errors | **None**; warnings logged by both `LogoStorage` and `QuotationPdfService` |
| Restored the file, regenerated | Back to 54,100 bytes, 1 image — byte-identical to the original |
| Build / tests | Clean, 0 warnings; **121/121 passing** |

**Limitation, stated plainly:** the PDF could not be *visually* rendered for inspection — `pdftoppm`
is not installed and the preview browser's PDF plugin renders blank in this environment. Verification
was structural (valid header, byte size, embedded image count, no exceptions) rather than visual.
The layout should be eyeballed by a human before this is considered done.

### Accepted

- **Degrade, never fail.** A quotation that cannot be sent because a logo moved is far worse than
  one sent without a logo.
- **Warnings at both layers**, so a dangling path is discoverable in logs rather than silent.
- **Image-object count as the assertion**, rather than only the status code — it proves the image
  was actually omitted rather than rendered blank or as a broken placeholder.
- **Accent colour falls back** on a malformed stored value instead of throwing mid-render.

### Changed beyond the request

- **Built the entire PDF feature**, since the two follow-ups presupposed it.
- **Added `ILogoStorage.TryReadAsync`** — Infrastructure cannot reach `wwwroot` directly, and this
  keeps the missing-file policy in one place.
- **Defensive catch in the PDF service**, per the gap above.
- **PDF buttons on the list and detail views**, otherwise the endpoint is URL-only.

### Rejected

- Did not enforce dimensions at upload. Capping at render keeps the original intact and lets each
  document choose its own box.
- Did not add image resizing or re-encoding; that needs an imaging dependency and QuestPDF already
  scales cleanly.
- Did not implement the old `IPdfGenerator` placeholder interface — its `RenderAsync(string
  templateName, object model)` signature is stringly-typed and untyped. `IQuotationPdfService` is
  specific and type-safe. **`IPdfGenerator` is now dead code and should be deleted.**

### Open items after this step

- **`IPdfGenerator` is unused** and should be removed.
- The PDF layout has not been visually reviewed — see the limitation above.
- No PDF for anything other than quotations.
- Rendering is synchronous and in-process; a large batch would tie up request threads.
