# Acceptance Criteria

> **Partial.** Only criteria explicitly stated so far are listed. The full set depends on
> `requirements-analysis.md`, which is still unwritten — no complete functional requirements have
> been provided. This file grows as criteria are stated.

## Core

| # | Criterion | Status | Verified |
| --- | --- | --- | --- |
| 1 | Data persists after an application restart | **Met** | 2026-07-18 |
| 2 | A user can log in with the seeded credentials | **Met** | 2026-07-18 |
| 3 | A user can log out | **Met** | 2026-07-18 |
| 4 | Protected pages are inaccessible when signed out | **Met** | 2026-07-18 |
| 5 | Invalid credentials produce a clear error, not a crash | **Met** | 2026-07-18 |

### 1. Data persists after an application restart

Seeded on first launch, then the application was stopped and restarted.

| | Companies | Products | Customers | Users | Roles |
| --- | --- | --- | --- | --- | --- |
| First launch | 1 | 8 | 7 | 1 | 3 |
| After restart | 1 | 8 | 7 | 1 | 3 |

Counts identical — data survived, and the seeder correctly skipped rather than duplicating
(`Seed skipped: database already contains a company.` in the startup log). Data was confirmed both
by direct SQL and by loading `/Products`, which listed all 8 seeded products.

### 2. A user can log in with the seeded credentials

`admin@inventoryerp.local` / `Admin@123456` signed in successfully at `/Account/Login`. The
navigation bar then showed the username and a Log out button, and `/Products` rendered the seeded
data.

Requesting `/Products` while signed out redirected to `/Account/Login?ReturnUrl=%2FProducts`;
after a successful sign-in the browser landed back on `/Products`, so the return-URL round trip
works.

### 3. A user can log out

The nav "Log out" control is a POST form with an antiforgery token, not a link. Clicking it ended
the session and redirected to `/Account/Login`; `/Products` then redirected to login again,
confirming the cookie was cleared.

### 4. Protected pages are inaccessible when signed out

Verified by direct HTTP request while unauthenticated:

| Path | Result |
| --- | --- |
| `/` | 302 → `/Account/Login?ReturnUrl=%2F` |
| `/Home/Privacy` | 302 → `/Account/Login?ReturnUrl=%2FHome%2FPrivacy` |
| `/Products` | 302 → `/Account/Login?ReturnUrl=%2FProducts` |
| `/Account/Login` | 200 |

Enforced by a global fallback authorization policy, so protection is the default rather than
something each controller must opt into.

### 5. Invalid credentials produce a clear error

Submitting the correct email with a wrong password redisplayed the form with *"Invalid email or
password."* in an alert — no exception, no stack trace. The same message is returned for an unknown
account and an inactive account, so the form cannot be used to enumerate valid email addresses.

## Not yet stated

Criteria for product, customer, company and quotation management have not been provided. Known
functional gaps that would likely become criteria are tracked in `README.md` and
`implementation-plan.md` — most significantly that tenant isolation is not enforced.
