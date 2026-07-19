# Test Strategy

## Tooling

| Tool | Version | Purpose |
| --- | --- | --- |
| xUnit | 2.9.3 | Test framework |
| `Microsoft.EntityFrameworkCore.InMemory` | 10.0.10 | In-memory provider for service tests |
| `Microsoft.NET.Test.Sdk` | 17.14.1 | Test host |
| coverlet.collector | 6.0.4 | Coverage collection |

**FluentAssertions is deliberately not used.** Version 8 requires a paid licence for commercial use,
and this is a commercial ERP. Tests use plain xUnit assertions. If fluent syntax becomes desirable,
AwesomeAssertions is the free fork of the v7 API.

## What is tested where

### Mandatory acceptance tests — `Acceptance/`

Two required scenarios, kept in their own folder so they are easy to find and run alone:

```bash
dotnet test --filter "FullyQualifiedName~Acceptance"
```

- **`QuotationCalculationTests`** — a two-line quotation whose expected totals are hand-calculated
  in comments, plus zero/negative-quantity rejection.
- **`SettingsDefaultsTests`** — documented defaults with no setting rows, and PDF generation
  surviving partial, blank, malformed and missing-file settings.

### Unit tests — `tests/InventoryErp.Application.Tests`

- **`Common/ServiceResultTests`** — the result pattern itself: that each factory sets the right
  `Status`, `IsSuccess`, payload and error state. This is foundational; every service returns one.
- **`Services/ProductServiceTests`** — application service behaviour against an in-memory
  `InventoryErpDbContext`: create, duplicate SKU, blank SKU, not-found, update, soft delete, and
  per-tenant SKU scoping.

Each test constructs its own context with a unique database name, so tests are isolated and can run
in parallel.

### What the in-memory provider cannot cover

This is the main limitation of the current approach. The InMemory provider is not a relational
database — it ignores:

- Filtered unique indexes, including the `(CompanyId, Sku)` and `Code IS NOT NULL` constraints
- `decimal(18,2)` / `decimal(5,2)` precision and any rounding or truncation behaviour
- Foreign keys, cascade and restrict delete behaviour
- Column max lengths

So `CreateAsync_returns_Conflict_for_a_duplicate_sku` passes because of the explicit guard in
`ProductService`, **not** because the database rejected it. Both matter, and only one is currently
under automated test.

### Manual and SQL verification

Schema-level behaviour is currently verified by hand against real SQL Server and recorded in
[test-results.md](test-results.md). This covers cascade/restrict FK behaviour, the nullable unique
index, and end-to-end persistence through the running application.

## Tests Not Covered (and why)

Honest scope boundaries. These are **absent**, not overlooked.

### Out of scope for Core

| Not covered | Why |
| --- | --- |
| **UI / browser automation** | No Playwright or Selenium suite. Every screen was verified manually and by screenshot; automating it is a project of its own and Stretch scope. The cost is that a CSS or Razor regression is caught only by a human looking |
| **Controller / HTTP integration tests** | No `WebApplicationFactory` suite. Controllers are thin — they resolve a company, call a service, map the result — so the logic worth testing sits in the services, which are covered. Routing, model binding and authorisation filters are consequently untested in CI |
| **Database constraints** | Tests use the EF **in-memory provider**, which ignores filtered unique indexes, FK cascade/restrict, column lengths and `decimal` precision. So `CreateAsync_returns_Conflict_for_a_duplicate_sku` passes on the *service guard*, not the unique index. Both matter; only one is automated. Schema behaviour was verified by hand with `sqlcmd` and is recorded in `test-results.md` |
| **PDF visual layout** | Tests assert the bytes are a valid PDF and that image objects appear or disappear. They cannot assert it *looks* right — no renderer is installed, so `pdftoppm`-based inspection is unavailable. Layout needs a human eye |
| **Authentication flows** | Login, logout, lockout and the authorisation fallback policy are verified manually only |
| **Concurrency** | The known quotation-numbering race is documented, not tested. Reproducing it reliably needs parallel requests against a real database |
| **Migrations** | No test asserts that migrations apply cleanly to an empty database. Verified manually once |

### Deliberately thin

| Area | Rationale |
| --- | --- |
| **GST rounding edge cases** | `QuotationCalculatorTests` covers the four Indian slabs, a 100% discount, and one explicit half-up midpoint (`0.125 → 0.13`). It does **not** sweep many awkward fractions, exercise very large amounts, or test rounding accumulation across dozens of lines. The invariant that matters — header totals equal the sum of line totals — is asserted, which catches drift without enumerating cases |
| **Paging** | Boundaries are covered (page past the end, out-of-range arguments) but not large datasets. Nothing has been tested beyond a few dozen rows |
| **Search relevance** | Matching is tested; ranking is not, because there is no ranking — results are grouped by type, not scored |
| **Coverage threshold** | None enforced. A percentage on this suite would measure the wrong thing; the gaps above are structural, not a matter of line coverage |

### What would close the biggest gap first

Integration tests against a real SQL Server via Testcontainers. That single change would cover
filtered unique indexes, cascade/restrict behaviour, decimal precision and migration application —
currently the largest cluster of untested behaviour, and the one where the in-memory provider is
actively misleading rather than merely silent.

## Conventions

- Test names read as sentences: `MethodName_expected_behaviour_in_context`.
- One behaviour per test; no shared mutable state between tests.
- Tests assert on observable outcomes (the returned `ServiceResult`, database row counts) rather
  than on internal calls.
