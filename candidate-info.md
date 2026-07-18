# Candidate Info

## Candidate

| Field | Value |
| --- | --- |
| Name | Aashish Choudhry |
| Email | aashishchaudhary.26@gmail.com |
| Repository | `inventory-management` (branch `main`) |
| Start date | 2026-07-18 |

## Project Summary

An SME ERP / inventory management application built on **.NET 10** using **Clean Architecture**.

The solution separates concerns across six projects, with the dependency rule enforced by project
references rather than convention — `InventoryErp.Domain` has no references at all, and dependencies
point inward toward it.

| Project | Responsibility |
| --- | --- |
| `InventoryErp.Domain` | Entities, enums, repository/unit-of-work interfaces. No dependencies. |
| `InventoryErp.Application` | DTOs, service interfaces, the `ServiceResult<T>` pattern. |
| `InventoryErp.Infrastructure` | EF Core (SQL Server), ASP.NET Identity, service implementations. PDF generation later. |
| `InventoryErp.Shared` | Constants and settings keys. |
| `InventoryErp.Web` | Razor MVC frontend and composition root. |
| `InventoryErp.Application.Tests` | xUnit test suite. |

### Key architectural decisions

- **`ServiceResult<T>` instead of exceptions** for expected failures. Application services return a
  result carrying a `ResultStatus` (`Success`, `NotFound`, `ValidationFailed`, `Conflict`,
  `Unauthorized`, `Error`); exceptions are reserved for genuinely unexpected faults. The web layer
  maps these statuses onto HTTP responses.
- **Identity registration lives in the Web composition root**, not Infrastructure, because
  `AddDefaultUI()` would otherwise drag an ASP.NET framework reference into the Infrastructure layer.
- **Soft deletes** via `BaseEntity.IsDeleted` plus a global EF query filter, so deleted rows stay
  auditable but are invisible to normal queries.
- **Audit stamping** (`CreatedBy` / `ModifiedBy`) is applied in `SaveChangesAsync` using an
  `ICurrentUser` abstraction implemented by the web layer from the HTTP context.

## Tools Used So Far

| Tool | Version / Model | Used for |
| --- | --- | --- |
| .NET SDK | 10.0.302 | Solution scaffold, build, test |
| Entity Framework Core | 10.0.10 | ORM, migrations (`dotnet-ef` 10.0.10) |
| ASP.NET Core Identity | 10.0.10 | Authentication and user management |
| SQL Server 2022 | `.` (local default instance) | Development database |
| xUnit | 2.9.3 | Unit testing |
| Claude Code | Opus 4.8 | Planning, scaffolding, implementation, end-to-end verification |
| Git | — | Version control |

### Notes on tool choices

- **FluentAssertions was deliberately not used.** Version 8 requires a paid commercial license, and
  this is a commercial ERP project. Tests use plain xUnit assertions instead.
- The solution file is **`InventoryErp.slnx`**, the XML-based format that is the default in .NET 10.
  It works with the .NET CLI and current Visual Studio / Rider, but not with older tooling.
