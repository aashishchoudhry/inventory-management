# UI Flow

> **Status:** only the Product flow exists. `Company`, `Customer` and `Quotation` have no UI yet.

## Stack

ASP.NET Core Razor MVC with the default Bootstrap 5 template and the scaffolded ASP.NET Identity UI
(Razor Pages, `/Identity/Account/*`).

## Authentication

```
any /Products/* request
   └─ not signed in → redirect to /Identity/Account/Login
                        ├─ Login  → returns to the requested page
                        └─ Register → creates account, signs in, redirects to /
```

`ProductsController` carries a bare `[Authorize]`, so any authenticated user has full access. Role
restrictions are not applied — the roles in `Roles.All` are declared but never seeded, so
`[Authorize(Roles = ...)]` would currently deny everyone.

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
| `/Products` | `Index.cshtml` | Table of SKU, name, selling price, GST %, stock, status. Shows a "Reorder" badge when `CurrentStock <= ReorderLevel`. Empty state: "No products yet." |
| `/Products/Create` | `Create.cshtml` | Full form. **`CompanyId` is currently a raw text input** — a placeholder pending tenant context |
| `/Products/Edit/{id}` | `Edit.cshtml` | Same fields; `Id` and `CompanyId` hidden |
| `/Products/Details/{id}` | `Details.cshtml` | Read-only definition list, plus Edit and Delete |

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

The layout header links Home, Products and Privacy, with `_LoginPartial` on the right showing either
Register/Login or the current username and Logout.

## Known UI gaps

- **`CompanyId` is a user-editable text field** on the Create form. A user can type any Guid and
  write into another tenant's data. It needs to come from tenant context, not the form.
- No company, customer or quotation screens.
- No search, filtering, sorting or pagination on the product list — it renders every row.
- No confirmation dialog before delete; the button posts immediately.
- No success/flash messaging after create, edit or delete — the user is redirected with no feedback.
- Default template styling throughout; no design pass.
