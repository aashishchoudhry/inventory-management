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

## Customers screen

`/Customers` — `CustomersController.Index` + `Views/Customers/Index.cshtml`.

**Read-only.** No create, edit, delete or row actions — customer maintenance is Stretch scope, and
the screen does not hint at actions that do not exist.

```
/Customers                     all customers, page 1
/Customers?page=2              page 2
/Customers?pageSize=3          explicit page size (max 200)
```

### Columns

Name, code, mobile, city, state. Code and mobile show an em-dash when absent — the seeded
"Walk-in / Counter Sales" customer has neither, which exercises that path.

### States

| State | Appearance |
| --- | --- |
| **Success** | Table ordered by name. Header reads "Showing 1–7 of 7" |
| **Loading** | None — server-rendered, so there is no intermediate state |
| **Empty — no customers** | `_EmptyState` partial: "No customers yet". **No call-to-action button**, since there is no create screen to link to |
| **Empty — page past the end** | "Page 4 is empty · There are only 7 customers in this list" with *Back to first page* |
| **Error** | Red alert; the table is not rendered. Covers no company configured, and invalid paging |

There is deliberately **no search box**, unlike Products — search was not in scope for this screen,
and an input that filters nothing would be worse than none.

### Paging

Identical to Products: the pager appears only when there is more than one page, Previous/Next are
disabled at the ends and carry `pageSize` through. Invalid arguments are rejected by the service
rather than clamped.

## Quotation screens

Three screens, all under `QuotationsController`.

```
/Quotations                    list, newest first
/Quotations/Create             GET form, POST submit
/Quotations/Details/{id}       full quotation with lines
```

### Create — `/Quotations/Create`

A customer dropdown, dates, notes, and a **dynamic line-item table**: add and remove rows
client-side, each with product, quantity, unit price, discount % and GST %.

| Behaviour | Detail |
| --- | --- |
| Add / remove rows | Vanilla JS clones a `<template>`. Field names are **renumbered to a contiguous `Lines[0..n]`** after every add or remove, so model binding never sees a gap |
| Product prefill | Selecting a product fills unit price and GST from the product — but **only into empty or zero fields**, so a negotiated price is never overwritten |
| Live total preview | Mirrors the server calculation (discount first, then GST). Labelled *"Preview only — the server recalculates on save"*, because it is |
| Totals are never submitted | The form posts only quantities, prices and percentages. Every monetary figure is computed server-side |

#### States

| State | Appearance |
| --- | --- |
| **Initial** | One blank line, so the form is usable without clicking "Add line" first |
| **Loading** | None — server-rendered. The submit button shows a spinner while in flight |
| **No lines** | If every row is removed, an inline note appears: "No lines yet. Add at least one before saving." The server also rejects it |
| **Validation error** | Re-renders the form with **all entered values preserved** — customer, dates, notes and every line. Line-level errors appear **next to the offending input**, not only in a summary |
| **Success** | Redirects to the detail view with a flash: "Quotation QT-2026-0001 was created." |
| **No company configured** | Error banner instead of the form |

#### Error mapping

`ServiceResult.ValidationErrors` entries prefixed `Line N:` are parsed and attached to that row's
specific field, matched on the message text (quantity / product / unit price / discount / GST).
Anything unrecognised falls back to the summary, so no error is ever silently dropped.

Verified messages: *"Select a customer."*, *"A quotation must have at least one line."*,
*"Quantity must be greater than zero."*, *"Discount percent must be between 0 and 100."*

> `CustomerId` and `Lines[i].ProductId` are **nullable `Guid?`** in the form model. As non-nullable
> `Guid` they failed model binding before `[Required]` could run, producing the framework's
> unhelpful *"The value '' is invalid."* — exactly the generic message this screen is meant to avoid.

### List — `/Quotations`

Columns: number, customer, date, valid until, line count, total. Newest first — a quotation list is
a work queue, not a reference table.

| State | Appearance |
| --- | --- |
| **Success** | Table with "Showing 1–1 of 1" |
| **Empty** | "No quotations yet" with a *New quotation* call to action |
| **Page past the end** | "Page N is empty" with *Back to first page* |
| **Error** | Red alert; table not rendered |

### Details — `/Quotations/Details/{id}`

Line table (product with SKU, qty, unit price, discount %, GST %, tax, total), a summary card
(subtotal, discount, GST, total), a details card, and notes when present.

| State | Appearance |
| --- | --- |
| **Success** | Full quotation. An expired `ValidUntil` shows an "Expired" pill |
| **Not found / other tenant** | `404` — another company's quotation is indistinguishable from a non-existent one, so ids cannot be probed |
| **Deleted product on a line** | Renders "(deleted product)" rather than failing |

No PDF action yet — that is the next step.

## Settings screen

`/Settings` — `SettingsController.Index` (GET and POST), one form covering everything.

Grouped into cards: **Company** (name, tagline, mobile, email, website), **Registered address**,
**Tax identifiers** (GSTIN, PAN), **Document text** (invoice terms, footer), **Logo**, and
**Branding** (accent colour).

Uses **POST-redirect-GET**: after a successful save the browser is redirected back to a fresh GET,
so a refresh cannot resubmit and the page genuinely re-reads from the database rather than echoing
what was posted.

### Logo upload control

A file input accepting `image/png,image/jpeg`, capped at **2 MB**, in its own card.

| Element | Behaviour |
| --- | --- |
| Preview | When a logo exists, it renders in a bordered panel above the input |
| **Remove current logo** checkbox | Only shown when a logo exists. Clears it on save |
| File input | Choosing a file replaces the current logo on save |
| Hidden `LogoPath` | Round-trips the stored path, so saving without choosing a file keeps the existing logo |

The form carries **`enctype="multipart/form-data"`** — without it the file is silently not posted.

**Validation is server-side and layered.** The `accept` attribute and the size hint are
conveniences; the server independently checks extension, size, **and the file's magic bytes**. A
text file renamed to `.png` with `Content-Type: image/png` is rejected — the extension and content
type are both client-supplied and trivially forged.

Files are stored under `wwwroot/uploads/logos/` with a **generated GUID filename**; the uploaded
name is never used. Replacing a logo deletes the previous file, but only *after* the save succeeds,
so a failed save never leaves the company with a missing image.

### States

| State | Appearance |
| --- | --- |
| **Initial** | Fields pre-filled from settings, falling back to the `Company` columns; "No logo uploaded yet." when none |
| **Loading** | None — server-rendered. Submit shows a spinner |
| **Success** | Redirect, then a green "Settings saved." flash |
| **Validation error** | Form re-renders with entered values intact; messages beside the offending field |
| **Rejected upload** | Message under the file input; **nothing is saved**, and the existing logo and unsaved edits are both preserved |
| **No company configured** | Error banner instead of the form |

Verified rejection messages: *"The logo must be a PNG or JPG image."*, *"That file is not a valid
PNG or JPG image."*, *"The logo must be 2 MB or smaller."*

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
| `/Customers` | `Customers/Index.cshtml` | Read-only paged table. See the dedicated section below |
| `/Quotations` | `Quotations/Index.cshtml` | Paged list, newest first |
| `/Quotations/Create` | `Quotations/Create.cshtml` | Dynamic line-item editor with live preview |
| `/Quotations/Details/{id}` | `Quotations/Details.cshtml` | Full quotation with lines and totals |
| `/Account/Login` | `Account/Login.cshtml` | Centred auth card. See states above |
| `/Account/AccessDenied` | `Account/AccessDenied.cshtml` | Centred state with icon and route back |
| `/Settings` | `Settings/Index.cshtml` | Company settings form, including logo upload. See below |
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

The sidebar is grouped: **Overview** (Dashboard), **Inventory** (Products), **Sales** (Customers,
Quotations). Active state is derived from `ViewContext.RouteData`, so a page added later highlights
without editing the partial. Company settings is deliberately absent — no controller exists for it,
and a nav link to a 404 is worse than no link.

The top bar holds the drawer toggle (below `lg`), the page title, **the global search box**, the
theme toggle and the user dropdown.

## Global search

**Available from every page** — it lives in the shared top bar, not on a screen of its own. There is
no "Search" sidebar entry, because search is a way to move around the app rather than a place in it.

### Both a dropdown and a page, deliberately

| Surface | Route | Purpose |
| --- | --- | --- |
| Nav dropdown | `GET /Search/Suggest?q=` (JSON) | Jump straight to a record you already know exists — the common case |
| Results page | `GET /Search?q=` (HTML) | Browse many matches, share the URL, or work without JavaScript |

The two answer different questions. A dropdown alone cannot show more than a handful of hits or be
linked to; a page alone costs a full navigation every time you want to open a product you can
already name. The nav `<form>` GETs to the results page, so the dropdown is a **progressive
enhancement** — with JavaScript off, typing and pressing Enter still works.

### Behaviour

| Aspect | Detail |
| --- | --- |
| Minimum length | **2 characters.** Enforced in the service *and* client-side, which simply avoids pointless requests |
| Debounce | 200 ms after the last keystroke |
| Stale responses | Each request aborts the previous one, so a slow early keystroke cannot overwrite a later, more specific result |
| Grouping | Results grouped by type with a heading, each row showing title, subtitle and a right-aligned value |
| Keyboard | ↓/↑ move, Enter opens, Escape closes. **Ctrl/Cmd + K** focuses the box |
| Footer | "See all N results" links to the full page |

### States

| State | Dropdown | Results page |
| --- | --- | --- |
| **Under 2 characters** | Stays closed; no request sent | "Enter at least 2 characters to search." |
| **No keyword** | — | Prompt listing what is searchable |
| **Loading** | Previous results stay visible until replaced, so the panel does not flicker | Server-rendered; no intermediate state |
| **Results** | Grouped, capped at 5 per type | Grouped, capped at 25 per type, with per-type counts |
| **Truncated** | "See all N results" | "Showing the first matches of each type…" |
| **No match** | "No results for *keyword*" | Empty state with the term echoed |

### What each result links to

Products and quotations open their detail page. **Customers link to the customer list**, because no
per-customer detail screen exists yet — handled explicitly in `SearchResultRoutes.HasDetailPage`
rather than silently producing a dead link, and the button reads "View list" instead of "Open".

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
