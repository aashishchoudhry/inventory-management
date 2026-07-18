# Tool Workflow

How AI tooling has been used on this project, and the working pattern that emerged.

## Primary tool

**Claude Code (Opus 4.8)**, run from the terminal against the repository.

## The loop

Each step of work followed roughly the same shape:

1. **Prompt with a scoped task** — one concern at a time (two entities, one layer, one migration)
   rather than "build the app".
2. **Tool inspects existing state first.** Repeatedly this caught conflicts before code changed —
   an entity that already existed, packages already installed, a DbContext already wired.
3. **Conflicts surfaced as a question**, not resolved silently. See below.
4. **Implementation**, propagating the change across every affected file so the build stays green.
5. **Verification** — build, tests, and where relevant direct SQL or driving the running app.
6. **Documentation** updated in the same step, while the reasoning was fresh.
7. **Commit** with a message recording the decision and its rationale.

## What worked

- **Small scoped steps.** Each step ended with a clean build and passing tests, so a mistake was
  never more than one step deep.
- **Stopping on conflict rather than guessing.** Four times the request did not match the
  repository's actual state — the `Product` field-set conflict, the pre-existing DbContext, the
  already-installed EF packages, and the migration name collision. Each was raised before anything
  was overwritten. Silently "fixing" any of them would have destroyed working code.
- **Verifying behaviour, not exit codes.** A migration that generates and applies cleanly can still
  encode the wrong delete behaviour. Cascade and restrict were checked with real SQL; the
  `ServiceResult` pattern was checked by driving the running app and seeing a form message rather
  than a stack trace.
- **Recording gaps as they appeared.** Tenant isolation, soft-delete/cascade interaction and the
  unimplemented `IPdfGenerator` were all written down at the moment they were noticed.

## What needed correcting

- **Assuming a clean slate.** Several prompts described work that was already done. The habit of
  checking first, rather than executing literally, prevented overwrites — but it means prompts
  should be written against the current repository state.
- **Defaults that were mine, not chosen.** The database instance is the clearest case: LocalDB was a
  reasonable default picked during scaffolding, and it silently determined where the schema was
  created. It surfaced only when the connection string changed. Defaults chosen by the tool need
  flagging as decisions, not left implicit.
- **Documentation drift.** Renaming the DbContext and switching database instance both left stale
  references across several markdown files that had to be swept afterwards.

## Verification methods used

| Method | Used for |
| --- | --- |
| `dotnet build` with warnings-as-errors | Catching issues at compile time |
| `dotnet test` | Service and result-pattern behaviour |
| `dotnet ef dbcontext info` | Validating the EF model without generating a migration |
| Direct `sqlcmd` | Schema behaviour the in-memory provider cannot cover |
| Driving the running app in a browser | End-to-end behaviour through the real HTTP pipeline |

## Where prompts are recorded

`ai-prompts/`, by phase — `planning.md`, `design.md`, `implementation.md`, `testing.md`,
`debugging.md`, `code-review.md`, `documentation.md`. Each entry records the prompt, what the tool
did, and what was accepted, changed or rejected.
