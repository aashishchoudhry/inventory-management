# UI Flow

> **Status:** only the Product flow exists. `Company`, `Customer` and `Quotation` have no UI yet.

## Stack

ASP.NET Core Razor MVC on **Bootstrap 5.3.3** (vendored, no CDN, no Node build step), with a custom
design-token layer. jQuery 3.7.1 and jquery-validation are used for unobtrusive client validation
only; all other interactivity is vanilla JS in `wwwroot/js/site.js`.

## Design system

| Layer | File | Purpose |
| --- | --- | --- |
| Tokens + components | `wwwroot/css/theme.css` | Overrides Bootstrap's own CSS variables, so built-in components inherit the theme rather than needing per-component rules |
| Overrides | `wwwroot/css/site.css` | Thin; a handful of exceptions only |
| Scoped | `Views/Shared/_Layout.cshtml.css` | Footer only. The template's hardcoded colours were removed — they broke dark mode |

### Theming

Light and dark, via Bootstrap's native `data-bs-theme`. The preference resolves in this order:
`localStorage` → `prefers-color-scheme` → light. An inline script in `<head>` applies it **before
first paint**, so a dark-mode user never sees a flash of light theme. The toggle sits in the top bar
and reflects state through `aria-pressed`.

### Two layouts

| Layout | Used by | Shape |
| --- | --- | --- |
| `_Layout` | All authenticated pages | Fixed sidebar + sticky top bar + content + footer |
| `_AuthLayout` | Login, access denied, error | Centred card, no nav |

Splitting them matters: previously the login page rendered the full app navigation, every link of
which bounced straight back to login.

### Shared partials

`_Sidebar`, `_TopBar`, `_LoginPartial` (user dropdown with POST logout), `_Alerts` (TempData flash),
`_EmptyState`, `_IconSprite`.

Icons are an inline SVG sprite rendered once per page and referenced with `<use href="#i-name">`.
Inline rather than an external `.svg` file because cross-document `<use>` has inconsistent browser
support; inline rather than an icon font because there is no external host.

## Interactivity

All vanilla JS in `wwwroot/js/site.js`, each module guarded so a page missing its elements is a
no-op rather than an error:

| Feature | Behaviour |
| --- | --- |
| Theme toggle | Flips `data-bs-theme`, persists to `localStorage`, follows OS while no explicit choice is stored |
| Sidebar drawer | Off-canvas below `lg`, backdrop, closes on Escape / backdrop click / nav click |
| Table filter | Client-side row filtering with a live "N of M items" count |
| Password reveal | Toggles input type, swaps icon, updates `aria-label` |
| Submit busy state | Disables the button and shows a spinner; skips when client validation fails |
| Delete confirmation | Bootstrap modal wired from `data-*` attributes |
| Flash auto-dismiss | Success and info dismiss after 6s; **errors never auto-dismiss** |

## Authentication

Authentication is **deny-by-default**. A global fallback authorization policy requires an
authenticated user for every endpoint; only pages marked `[AllowAnonymous]` are reachable signed
out. A new controller is therefore protected automatically, rather than protected only if someone
remembers the `[Authorize]` attribute.

| Endpoint | Anonymous? |
| --- | --- |
| `/Account/Login` (GET, POST) | Yes |
| `/Account/AccessDenied` | Yes |
| `/Home/Error` | Yes — so an error for a signed-out user shows the error, not a login redirect |
| Everything else | No |

```
any protected request
   └─ not signed in → 302 /Account/Login?ReturnUrl=<original path>
                        └─ correct credentials → back to the original path
```

The scaffolded ASP.NET Identity default UI has been **removed** — no registration, password reset,
email confirmation or account-management pages. `/Identity/Account/*` routes no longer exist.

Roles (`Admin`, `Manager`, `StoreKeeper`) are seeded and the seeded user holds `Admin`, but no
controller restricts by role yet — `ProductsController` inherits the plain authenticated-user
requirement.

## Login screen

`/Account/Login` — `AccountController` + `Views/Account/Login.cshtml`.

Fields: email, password, "Remember me". No registration or forgot-password links, since those
flows do not exist.

### States

| State | Appearance |
| --- | --- |
| **Empty** (first load) | Email and password blank, no error alert. Email field autofocused |
| **Field validation** | Missing or malformed email, or missing password, caught client-side by unobtrusive validation; message under the offending field |
| **Bad credentials** | Red alert above the form: *"Invalid email or password."* Email is preserved, password cleared. Identical message whether the account does not exist, the password is wrong, or the account is inactive — so the form cannot be used to enumerate valid email addresses |
| **Locked out** | *"This account is locked. Try again later."* After 5 failed attempts, for 15 minutes |
| **Success** | 302 to `ReturnUrl` if it is a local path, otherwise to `/`. Nav switches to show the username and a "Log out" button |
| **Already signed in** | GET `/Account/Login` redirects to `/` rather than showing the form again |

### Logout

A **POST** form in the nav bar, not a link, with an antiforgery token. A GET logout can be triggered
by any third-party `<img>` or anchor pointing at the URL. On success the user returns to
`/Account/Login`.

## Products screen

`/Products` — `ProductsController.Index` + `Views/Products/Index.cshtml`.

Listing and search are **one action and one URL**, because a search is just a filtered list. The
form uses **GET**, so a search is a shareable, bookmarkable URL and the browser back button behaves:

```
/Products                            all products, page 1
/Products?q=helmet                   filtered
/Products?q=helmet&page=2            filtered, page 2
/Products?pageSize=3                 explicit page size (max 200)
```

Search and paging happen **in the database**, not in the browser. An earlier iteration filtered rows
client-side with JavaScript over whatever had been rendered; that only ever searched the current
page and could not scale.

### Columns

Name, SKU, barcode, selling price, GST %, stock, status, row actions. Barcode shows an em-dash when
absent. Stock shows a warning pill when at or below the reorder level.

### States

| State | Appearance |
| --- | --- |
| **Success** | Table of results. Header reads "Showing 1–8 of 8", plus "matching *keyword*" when searching |
| **Loading** | None — the page is server-rendered, so there is no intermediate state. The submit button shows a spinner while the request is in flight |
| **Empty — no products at all** | `_EmptyState` partial: "No products yet" with a *New product* call to action |
| **Empty — search matched nothing** | Distinct state: "No products match *keyword*", guidance to try another term, and a *Clear search* button. Deliberately different from the above — the cause and the useful next action differ |
| **Empty — page past the end** | "Page 3 is empty · There are only 8 products in this list" with *Back to first page*. Fixes a bug found during testing that rendered "Showing 21–8 of 8" over an unexplained empty table |
| **Error** | Red alert with the message; the table is not rendered. Covers no company configured, and invalid paging arguments |

### Paging

The pager appears only when there is more than one page. Previous/Next are disabled at the ends and
carry `q` and `pageSize` through, so paging never silently drops the search term.

Invalid arguments are **rejected by the service, not clamped by the controller** — `?page=0` shows
"Page number must be 1 or greater." rather than silently showing page 1, so the user learns what
happened.

## Product flow

```
/Products  (Index)
   ├─ "New product" → /Products/Create
   ├─ "Details"     → /Products/Details/{id}
   └─ "Edit"        → /Products/Edit/{id}

/Products/Create ─ POST ─┬─ success           → redirect /Products
                         ├─ Conflict          → redisplay form with error message
                         └─ ValidationFailed  → redisplay form with field errors

/Products/Edit/{id} ─ POST ─┬─ success   → redirect /Products
                            ├─ NotFound  → 404
                            └─ Conflict  → redisplay form with error

/Products/Details/{id}
   ├─ "Edit"   → /Products/Edit/{id}
   └─ "Delete" → POST /Products/Delete/{id} → soft delete → redirect /Products
```

### Screens

| Route | View | Notes |
| --- | --- | --- |
| `/` | `Home/Index.cshtml` | **Dashboard** — four stat tiles (total products, active, below reorder, stock value) and a "Needs reordering" table. Data from the existing `IProductService`; no new service was added |
| `/Products` | `Products/Index.cshtml` | **Server-side** search and paging. See the dedicated section below |
| `/Products/Create` | `Create.cshtml` | Grouped sections (Identification / Pricing / Stock), two-column on `md+`. **`CompanyId` is a raw text input** marked with an amber border and an explanatory hint — a visible placeholder pending tenant context, deliberately not disguised as finished |
| `/Products/Edit/{id}` | `Edit.cshtml` | Same shape; `Id` and `CompanyId` hidden |
| `/Products/Details/{id}` | `Details.cshtml` | Detail card with computed GST-inclusive price, a stock card, and a "Danger zone" delete behind a confirmation modal |
| `/Account/Login` | `Account/Login.cshtml` | Centred auth card. See states above |
| `/Account/AccessDenied` | `Account/AccessDenied.cshtml` | Centred state with icon and route back |
| `/Home/Privacy` | `Privacy.cshtml` | Placeholder content, styled consistently |
| Error | `Shared/Error.cshtml` | Uses `_AuthLayout`, since an error can be reached signed out |

## How `ServiceResult` maps to the UI

This is the pattern every future controller should follow. `ProductsController` translates the
result status rather than catching exceptions:

| `ResultStatus` | UI outcome |
| --- | --- |
| `Success` | Redirect to the list, or render the view |
| `NotFound` | `404` |
| `Unauthorized` | `403` |
| `ValidationFailed` | Redisplay the form; errors added to `ModelState` |
| `Conflict` | Redisplay the form with the conflict message (e.g. duplicate SKU) |
| `Error` | Problem response |

Verified end-to-end: submitting a duplicate SKU renders *"A product with SKU 'SKU-1001' already
exists."* above the form rather than producing a stack trace.

## Navigation

The sidebar lists Dashboard and Products, with active state derived from `ViewContext.RouteData` so
a page added later highlights without editing the partial. Customers, Quotations and Company are
deliberately absent — no controllers exist for them, and a nav link to a 404 is worse than no link.

The top bar holds the drawer toggle (below `lg`), the page title, the theme toggle and the user
dropdown.

## Responsive behaviour

Breakpoints are Bootstrap's. The `lg` boundary (992px) is where the shell changes shape.

| Width | Behaviour |
| --- | --- |
| `< 992px` | Sidebar becomes an off-canvas drawer with backdrop; forms single-column; action-bar buttons full-width; tables scroll inside `.table-responsive` |
| `< 768px` | Touch targets raised to a 44px minimum |
| `≥ 992px` | Persistent sidebar |
| `≥ 1200px` | Stat tiles four-across; product forms two-column with a settings sidebar |

Verified at 375, 768 and 1280px: the page itself never scrolls horizontally — wide tables scroll
within their own container.

## Accessibility

`aria-current="page"` on the active nav item, `aria-live="polite"` on the flash region,
`aria-pressed` on the theme toggle, `aria-expanded` on the drawer toggle, `visually-hidden` labels
on icon-only action columns, a single consistent `:focus-visible` ring, Escape closing the drawer
and modals, and `prefers-reduced-motion` honoured for all transitions.

## Known UI gaps

- **`CompanyId` is a user-editable text field** on the Create form. A user can type any Guid and
  write into another tenant's data. Now visually marked as provisional, but the underlying
  tenant-isolation problem is unchanged — this is a backend gap, not a styling one.
- No company, customer or quotation screens.
- Product search/filter is **client-side over all rows**; there is no server-side pagination or
  sorting, so this will not scale past a few hundred products.
- Dashboard covers products only — `Customer` and `Quotation` have no application services to
  aggregate.
- No column sorting on the products table.
