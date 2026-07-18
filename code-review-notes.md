# Code Review Notes

Findings from reviewing the codebase, and what was done about them.

---

## 2026-07-18 — Dead code: `IPdfGenerator` removed

**Finding:** `InventoryErp.Application/Interfaces/IPdfGenerator.cs` was unused. Nothing implemented
it, nothing injected it, and it was never registered in DI. A repository-wide search found exactly
one code reference: its own declaration.

**Origin:** created during the initial solution scaffold as a placeholder, at a point when PDF
generation was a known future requirement but no library had been chosen. It was documented at the
time as "no implementation registered and injecting this will fail until one is added" — accurate,
but that state persisted for the whole project.

**Superseded by** `IQuotationPdfService`, built when PDF generation actually landed.

### Why the placeholder was not implemented

The two signatures are the substance of the finding:

```csharp
// The placeholder — removed
Task<byte[]> RenderAsync(string templateName, object model, CancellationToken ct = default);

// What is actually used
Task<ServiceResult<GeneratedDocument>> GenerateAsync(Guid quotationId, Guid companyId, CancellationToken ct = default);
```

| Aspect | `IPdfGenerator` | `IQuotationPdfService` |
| --- | --- | --- |
| Template selection | `string templateName` — a typo compiles and fails at runtime | No template parameter; the method *is* the document |
| Input | `object model` — no compile-time guarantee the model matches the template | Typed ids; the service loads what it needs |
| Output | `byte[]` — the caller must know the content type and invent a filename | `GeneratedDocument` carries filename, content type and bytes together |
| Failure | Exceptions only | `ServiceResult`, consistent with every other service in the codebase |

The `(string, object)` pairing is the core problem: it is a **stringly-typed** contract where the
compiler cannot check that the named template and the supplied model agree. Both mistakes surface
only at runtime, in the code path that produces a customer-facing document.

Its generality was also unearned. It was designed for "many document types share one renderer", but
only one document type exists, and a second (an invoice) would want its own typed inputs rather
than another `object`. The abstraction predicted a requirement that never arrived in that shape.

**Action:** deleted. Build clean with zero warnings, 121/121 tests passing — confirming nothing
depended on it.

### Lesson

A placeholder interface asserting a design decision that has not been made yet is a liability, not
a head start. This one named a rendering strategy (string-keyed templates) roughly a dozen steps
before the library was chosen, and the eventual design went a different way. Interfaces are cheapest
to write **after** there is a caller and an implementation to shape them.

Documentation referencing it — `README.md`, `implementation-plan.md`, `design-notes.md`,
`tool-workflow.md`, `ai-prompts/*` — is historically accurate at the point it was written, so it was
left alone rather than rewritten; `design-notes.md` already records why the placeholder was not
implemented. Only `README.md`'s "Known gaps" entry described a *current* state and was updated.
