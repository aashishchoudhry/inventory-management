# Planning Prompts

---

## 2026-07-18 — Solution structure and repository scaffold

**Tool:** Claude Code (Opus 4.8)

### Prompt

> I'm starting a .NET 10 SME ERP / inventory management project using Clean Architecture. Set up the
> solution structure for me:
>
> - InventoryErp.Domain — entities, enums, interfaces, no dependencies on anything else
> - InventoryErp.Application — DTOs, service interfaces, using a ServiceResult<T> pattern for
>   success/failure instead of exceptions
> - InventoryErp.Infrastructure — EF Core, ASP.NET Identity, later PDF generation
> - InventoryErp.Shared — constants, setting keys
> - InventoryErp.Web — Razor MVC frontend
> - tests/InventoryErp.Application.Tests — xUnit

A follow-up prompt asked for the `dotnet` CLI commands to reproduce the scaffold from an empty
folder, and for the repository documentation folder structure.

### What Claude Code did

- Checked the environment first: empty repo, .NET SDK 10.0.302 installed.
- Asked two scoping questions before writing code — database provider, and whether to build a bare
  skeleton or include a working vertical slice.
- Produced a written plan before implementing.
- Scaffolded six projects with references wired so dependencies point inward, with
  `Directory.Build.props` centralising `TargetFramework` / `Nullable` / `ImplicitUsings` /
  `TreatWarningsAsErrors`.
- Built a complete `Product` vertical slice to prove the wiring, then verified it end to end: build
  clean, tests passing, migration applied, and the running app driven in a browser through
  register → login → create product → duplicate SKU.

### Accepted

- **Asking about scope before scaffolding.** The vertical-slice choice materially changed the output.
- **A working slice rather than empty projects.** It proved the `ServiceResult` pattern, Identity,
  audit stamping and the dependency rule actually functioned, rather than merely compiling.
- **`TreatWarningsAsErrors`** from the start, which kept the codebase at zero warnings throughout.
- **Dropping FluentAssertions** on the tool's recommendation — v8 requires a paid licence for
  commercial use. Plain xUnit assertions instead.

### Changed

- Target framework corrected from .NET 8 to **.NET 10** — the initial prompt mentioned both, and the
  installed SDK was 10.0.302.
- Identity registration moved from Infrastructure to the web composition root, because
  `AddDefaultUI()` is ASP.NET-only.

### Rejected

- Nothing rejected. `README.md` was left untouched, since it already contained exactly the top-level
  heading requested.

### Noted for later

- `dotnet new sln` produces `.slnx` on .NET 10, not `.sln`.
- The `Web → Infrastructure` reference is a deliberate composition-root concession.
- `IPdfGenerator` was created as an unimplemented placeholder pending a library choice.
- The database instance (`(localdb)\MSSQLLocalDB`) was a tool-chosen default. It later needed
  changing, and is the clearest example of an implicit default that should have been surfaced as a
  decision.
