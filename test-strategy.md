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

## Gaps and planned work

| Gap | Planned approach |
| --- | --- |
| Schema constraints untested in CI | Integration tests against real SQL Server (Testcontainers or a LocalDB fixture) |
| No controller/web-layer tests | `WebApplicationFactory` integration tests once more UI exists |
| No tests for `Company`, `Customer`, `Quotation` | Follow the `ProductServiceTests` shape as each service is built |
| Tenant isolation untested | A priority once `ICurrentTenant` exists — a test proving one tenant cannot read another's rows |
| No coverage threshold | Add once the suite is broader; a threshold on 14 tests would be noise |

## Conventions

- Test names read as sentences: `MethodName_expected_behaviour_in_context`.
- One behaviour per test; no shared mutable state between tests.
- Tests assert on observable outcomes (the returned `ServiceResult`, database row counts) rather
  than on internal calls.
