# Documentation Prompts

---

## 2026-07-18 — Bring the documentation up to date

**Tool:** Claude Code (Opus 4.8)

### Prompt

> update all the required md files as well.

Following a step that repointed the database from `(localdb)\MSSQLLocalDB` to `Server=.`.

### What Claude Code did

Read the request as covering two things: files made stale by the database change, and files that had
accumulated real content to record but were still bare headings.

**Updated for the database change** (already partly done in the preceding step):
`database/setup-notes.md`, `candidate-info.md`, `implementation-plan.md` — including the `sqlcmd`
examples, which all still named the LocalDB instance.

**Filled in from actual work done:**

| File | Content |
| --- | --- |
| `README.md` | Architecture, prerequisites, getting started, known gaps |
| `implementation-plan.md` | Entries 2–5, covering the three domain steps and the infrastructure step |
| `design-notes.md` | Layering, the Identity placement trade-off, why `ServiceResult` over exceptions |
| `test-strategy.md` | Tooling, coverage, and what the in-memory provider cannot test |
| `test-results.md` | All 14 tests itemised, plus manual SQL and end-to-end verification |
| `debugging-notes.md` | Seven problems with cause and fix |
| `ui-flow.md` | The Product flow and `ServiceResult` → UI mapping |
| `tool-workflow.md` | The working loop, what worked, what needed correcting |
| `ai-prompts/planning.md`, `design.md` | Prompt history with accept/change/reject |
| `ai-prompts/testing.md`, `debugging.md` | Pointers, noting no dedicated prompts yet |

**Deliberately left as bare headings**, with the reason given rather than filled with invented
content: `requirements-analysis.md`, `acceptance-criteria.md` (both need the project's actual
requirements, which have not been stated), `api-contract.md` (no API surface — MVC views, no REST
endpoints), `code-review-notes.md`, `review-fixes.md`, `pr-description.md` (no review or PR yet),
`reflection.md`, `final-ai-usage-summary.md` (end-of-project documents), and
`tool-specific/other-tool-workflow/*` (for a different tool).

### Accepted

- **Not fabricating content for documents with no real substance.** Writing plausible-sounding
  acceptance criteria without stated requirements would have produced a document that looked
  complete and was worthless — worse than an empty file, because it would not be revisited.
- **Recording gaps as prominently as achievements.** Every document names what is missing:
  the unenforced tenant isolation, the untested schema constraints, the `CompanyId` form field.
- **Sweeping stale references after a rename.** Both the `ApplicationDbContext` → `InventoryErpDbContext`
  rename and the database instance switch left references across several files.

### Changed

- Read "all the required md files" as "all files with something real to record" rather than
  "every file in the repository", and reported which were skipped and why.

### Rejected

- Nothing was rejected, but the scope was narrowed as described above rather than filling every file.
