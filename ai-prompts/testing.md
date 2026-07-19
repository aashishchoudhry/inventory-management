# Testing Prompts

---

## 2026-07-18 — Mandatory xUnit tests

**Tool:** Claude Code (Opus 4.8)

### Prompt

> Now add the two mandatory xUnit tests in tests/InventoryErp.Application.Tests:
>
> 1. Quotation calculation test — create a quotation with known line items (e.g. 2 products, known
>    quantity/unitPrice/discountPercent/gstPercent), assert the resulting subTotal, taxAmount,
>    discountAmount, and totalAmount match hand-calculated expected values. Also add a test that a
>    zero-quantity line item is rejected.
> 2. Settings defaults test — when no CompanySetting rows exist for a company, assert GetSettings
>    returns the documented defaults, and that the PDF context correctly reads whatever settings are
>    present (partial settings shouldn't crash it).
>
> Run `dotnet test` and give me the output.
>
> Then fill in test-results.md, update test-strategy.md's "Tests Not Covered (and why)" section,
> update acceptance-criteria.md, and update ai-prompts/testing.md.

### What Claude Code did

Created `tests/InventoryErp.Application.Tests/Acceptance/` — a separate folder from the per-service
suites, so the required tests are findable and runnable alone via
`dotnet test --filter "FullyQualifiedName~Acceptance"`.

**`QuotationCalculationTests`** (9 tests). Two products, known inputs, and the full arithmetic
written out as a comment block in the file so the expected values are demonstrably hand-derived
rather than copied from a debugger:

```
Hex Bolt:      10 × 24.50 = 245.00 → −10% = 24.50 → taxable 220.50 → ×18% = 39.69 → 260.19
Safety Helmet:  5 × 349.00 = 1745.00 → −0%  =  0.00 → taxable 1745.00 → ×5%  = 87.25 → 1832.25
Header: subTotal 1990.00, discount 24.50, tax 126.94, total 2092.44
```

**`SettingsDefaultsTests`** (9 tests). Fixture is a company with nothing but its required name.
Asserts the documented defaults, that no property is ever null, that a stored row overrides a
default, and that the PDF survives five degraded settings shapes.

### `dotnet test` output

```
Passed!  - Failed: 0, Passed: 173, Skipped: 0, Total: 173, Duration: 7 s
```

18 of those are the new acceptance tests. Suite went from 155 to 173.

### Accepted

- **Hand-calculated values in comments.** A test whose expectations were lifted from the
  implementation only proves the code equals itself. Writing the arithmetic out means a reviewer can
  check it without running anything — and these figures independently match the real quotation
  created through the browser earlier (`QT-2026-0001`).
- **Asserting persistence, not just the return value.** `Totals_are_persisted_not_only_returned`
  reads back from the store, so a correct DTO built over a wrongly-populated entity cannot pass.
- **A precondition test.** `No_setting_rows_exist_for_the_company` asserts the fixture really is
  empty; without it, the defaults test could pass for the wrong reason.
- **Testing the rejection writes nothing.** A quotation with one good line and one zero-quantity
  line must save neither — the interesting failure is a partial save, not the error message.

### Changed / added beyond the request

- **A dedicated `Acceptance/` folder.** The prompt said "add the two tests"; several were already
  covered by existing per-service suites (`Rejects_a_non_positive_quantity`,
  `GetSettings_falls_back_to_hard_defaults_for_keys_with_no_column`). Rather than claim those as the
  deliverable or duplicate them silently, they were rewritten as explicit, self-documenting
  acceptance tests in one place — findable by whoever is checking the requirement.
- **`Gst_is_charged_on_the_discounted_amount_not_the_gross`** — not asked for, but this is the single
  most consequential decision in the calculation and it deserves a test named after it.
- **`The_rejection_message_identifies_which_line_is_wrong`** — the UI relies on the `Line N:` prefix
  to attach errors to the right row, so the format is load-bearing, not cosmetic.
- **Five PDF resilience cases** rather than one. "Partial settings shouldn't crash it" has several
  distinct paths: absent rows, blank values, a malformed colour, and a missing logo file all reach
  the renderer differently.

### Rejected

- Did not delete the overlapping tests in `QuotationServiceTests` and `SettingsServiceTests`. They
  assert different things — invariants and per-method behaviour rather than fixed expected values —
  and removing working coverage to avoid apparent duplication would be a net loss.
- Did not add an exhaustive GST rounding sweep. The existing calculator tests cover all four Indian
  slabs plus an explicit midpoint case, and the header-equals-sum-of-lines invariant catches drift
  without enumerating fractions. Stated plainly in `test-strategy.md` rather than left implied.
- Did not add integration or UI tests. Out of Core scope, and now documented as such.

### Honest note on scope

`test-strategy.md` gained a **"Tests Not Covered (and why)"** section. The most important admission:
tests use the EF **in-memory provider**, which ignores filtered unique indexes, foreign-key cascade
and restrict behaviour, column lengths and decimal precision. So the duplicate-SKU test passes on
the service guard, *not* the database constraint. Schema behaviour was verified by hand with
`sqlcmd`, but it is not in CI — and the in-memory provider is actively misleading there rather than
merely silent. Integration tests against real SQL Server would close the largest gap.
