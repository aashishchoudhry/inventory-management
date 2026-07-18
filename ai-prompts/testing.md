# Testing Prompts

No prompt has yet been dedicated to testing — no "write tests for X" instruction has been given.

Tests written so far were produced as part of implementation prompts, where each step was expected
to leave the build green and the suite passing. Those prompts and outcomes are recorded in
[implementation.md](implementation.md).

Current state, as of 2026-07-18:

- 14 xUnit tests, all passing — 6 covering `ServiceResult`, 8 covering `ProductService`.
- Schema-level behaviour (cascade, restrict, filtered indexes) verified manually with `sqlcmd`
  rather than by automated test.

See [test-strategy.md](../test-strategy.md) for the approach and its known gaps, and
[test-results.md](../test-results.md) for the latest run.
