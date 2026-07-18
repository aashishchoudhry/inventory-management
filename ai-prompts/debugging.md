# Debugging Prompts

No prompt has yet been dedicated to debugging — no "this is broken, fix it" instruction has been
given.

Problems encountered so far arose during implementation and were diagnosed and fixed within those
steps. Seven are recorded with cause and fix in [debugging-notes.md](../debugging-notes.md):

1. `CS0509` — deriving from a sealed DTO
2. `AddDefaultUI()` unavailable in Infrastructure
3. `dotnet new sln` producing `.slnx`, not `.sln`
4. `dotnet ef` requiring the Design package in the startup project
5. Migration name collision with a stale `InitialCreate`
6. `QUOTED_IDENTIFIER` failures against filtered indexes
7. Database created on the wrong SQL Server instance

The last three are the ones most likely to recur.
