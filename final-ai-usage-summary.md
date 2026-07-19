# Final AI Usage Summary

Tool: **Claude Code (Opus 4.8)** throughout. Prompt-by-prompt records are in `ai-prompts/`;
`tool-workflow.md` covers the mechanics. This summarises what was actually AI-driven versus
human-directed at each phase.

## Phase-by-phase involvement

"AI-driven" means the AI produced the substance and I reviewed it. "Human-directed" means I set the
constraint, decision or scope and the AI executed within it.

| Phase | Split | What that meant in practice |
| --- | --- | --- |
| **Requirement analysis** | **Human-directed, thin** | Requirements came from the assignment brief and my own prompts, one feature at a time. There was no upfront requirements phase — `requirements-analysis.md` was written **at the end, reverse-engineered from the delivered code**, and is explicitly labelled as such. This is the weakest phase in the project and the ordering is the reason. |
| **Planning / design** | **Mixed, leaning AI** | I set scope per step ("service layer only, no UI yet"); the AI proposed the design and the rationale. Some of the most consequential decisions were AI-originated and I accepted them: `Guid` over `int` keys, stored-not-computed quotation totals, settings as key/value rows, `Restrict` over cascade. Where a decision affected two stores or crossed a layer boundary, the AI flagged it for me rather than choosing — the `Company`-columns drift is the clearest example. |
| **Implementation** | **AI-driven** | Nearly all production code. One prompt per build step, ~13 steps, committed per step. My role was scope control, sequencing, and rejecting expansions. The AI regularly did more than asked — usually usefully (magic-byte validation, "remove logo" checkbox, gitignoring uploads), occasionally not, which is what the per-step "Changed beyond the request" sections exist to make visible. |
| **Testing** | **AI-driven, human-triggered** | I asked for two mandatory tests; the AI wrote those plus a structured `Acceptance/` folder and expanded partial-settings resilience into five distinct cases. It also chose the *design* of tests well — asserting persistence rather than return values, and adding a precondition test so the defaults test couldn't pass for the wrong reason. Test **strategy** limits were AI-written and honest: `test-strategy.md` states plainly that the in-memory provider makes the duplicate-SKU test pass on the service guard, not the index. |
| **Debugging** | **Genuinely collaborative** | The clearest split in the project. **I found the symptoms; the AI found the causes.** Two of the nastiest bugs were found from screenshots I supplied (the unstyled app, the search-icon overlap), and the AI diagnosed both correctly — a global fallback policy 302-ing static assets, and a CSS shorthand winning at equal specificity. Where the AI verified its own work it sometimes checked the wrong thing (page text rather than asset status codes), which is how the unstyled app shipped at all. |
| **Code review** | **Weak — the honest answer** | There was **no dedicated review phase**. `ai-prompts/code-review.md` is essentially empty and `review-fixes.md` is still a stub. Findings arrived incidentally: the AI caught *contract* problems while editing adjacent code (the `LogoStorage` exception contract, the `Company` drift, `IPdfGenerator` as dead code), but the one real vulnerability was found by **me reading `acceptance-criteria.md`**, ten steps after the AI itself had documented it. |
| **Documentation** | **AI-driven, and the phase needing most correction** | Volume and depth were excellent — ~2,000 lines of prompt archive with per-step decision records, and design rationale genuinely worth reading. Accuracy over time was the problem: files contradicted each other on tenant isolation, the README failed on a clean checkout, and stale current-state claims persisted in files nobody was told to revisit. Every one of those was caught by grep sweeps, not by the AI noticing on its own. |

## Overall Takeaways

**AI was strongest where a decision had a stateable rationale.** Trust boundaries (which inputs are
attacker-controlled), contracts between layers (what happens when `ILogoStorage` throws something
unexpected), what a test actually proves. These are the places it produced work better than what I
asked for, and it could explain *why* — which is what made the output reviewable rather than
something to take on faith.

**It was weakest at holding a claim true over time.** Almost every documentation defect was a
statement that was accurate when written and never revisited. The AI would happily write "tenant
isolation is not enforced" in step 5 and "isolation is enforced and tested" in step 12 without ever
reconciling them. It has no standing sense that a previously written file is now wrong.

**Writing a gap down substituted for fixing it.** The most serious defect in the build was
*documented by the AI itself*, early, and then carried forward as a known gap through file after
file. It became something the project had acknowledged rather than something the project had to
resolve. A security gap in a list looks handled.

**Silent failure was the recurring shape of every real problem.** The tenant isolation hole, the
missing foreign keys, the unstyled app, the two tests that passed while asserting nothing about a
company that didn't exist — none produced an error, and the test suite was green through all of
them. Everything of consequence was found by looking at an artefact directly: a screenshot, a raw
SQL query, a schema dump, a document read end to end.

**Verification has to target the actual artefact, not a proxy for it.** The pattern that worked:
querying the database instead of the app's own reads, counting embedded images in the PDF instead of
checking the status code, deleting the logo file to prove the missing-file path really degraded,
reproducing the forged-`CompanyId` POST rather than reasoning that the fix must work. The pattern
that failed: reading page text and concluding the page was fine.

**On the workflow itself** — one prompt per step with docs updated in the same step is worth
keeping. The per-step Accepted / Rejected / Open items records were the single most useful artefact
when writing these final documents, because they're the only place decisions *not* taken were
recorded. What's missing is an adversarial step: a standing prompt after each feature whose only job
is to attack what was just built. Every serious defect here was invisible to the step that created
it.
