# Creating `Malume.sln` and Clean Architecture Projects in Visual Studio 2026

Root `.NET` governance (`global.json`, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`) is already configured for **`.NET 8.0`** and **Central Package Management (CPM)**.

Follow this checklist when creating `Malume.sln` and the C# projects in Visual Studio so everything aligns with the blueprint and builds cleanly on the first pass.

---

## 1. Solution & Folder Layout

Create a blank solution named **`Malume`** (`Malume.sln`) in the repository root (`c:\DEV\Malume`), then add solution folders `src` and `tests` backed by `src/` and `tests/` directories on disk:

### `src/` Projects

| Project Name | Visual Studio Template | Framework | Purpose |
| :--- | :--- | :--- | :--- |
| `Malume.Domain` | Class Library | `.NET 8.0` | Entities, Value Objects, Domain Events, State Machines (zero external dependencies) |
| `Malume.Application` | Class Library | `.NET 8.0` | CQRS handlers (MediatR), validators (FluentValidation), interfaces |
| `Malume.Infrastructure` | Class Library | `.NET 8.0` | EF Core SQL Server + `NetTopologySuite`, Hangfire, Paystack, FCM |
| `Malume.Shared.Contracts` | Class Library | `.NET 8.0` | Shared DTOs, SignalR hub contracts, shared validation rules |
| `Malume.Api` | ASP.NET Core Web API | `.NET 8.0` | REST Controllers, SignalR `TelemetryHub`, JWT Auth, Hangfire dashboard |
| `Malume.Web.Portal` | Blazor WebAssembly Standalone App | `.NET 8.0` | Multi-tenant Admin, Fleet Owner & School SGB portal |

### `tests/` Projects

| Project Name | Visual Studio Template | Framework | Purpose |
| :--- | :--- | :--- | :--- |
| `Malume.Domain.UnitTests` | xUnit Test Project | `.NET 8.0` | Pure domain, escrow math, QR/PIN derivation, state machine tests |
| `Malume.Application.Tests` | xUnit Test Project | `.NET 8.0` | CQRS handler & validator unit tests |
| `Malume.Api.IntegrationTests` | xUnit Test Project | `.NET 8.0` | API + real SQL Server spatial integration tests via Testcontainers |

---

## 2. Clean Architecture Project References

Enforce strict inward-pointing dependencies:

* `Malume.Domain` → *(no project references)*
* `Malume.Shared.Contracts` → *(no project references)*
* `Malume.Application` → `Malume.Domain`, `Malume.Shared.Contracts` *(optional if DTOs live in Shared.Contracts)*
* `Malume.Infrastructure` → `Malume.Application`
* `Malume.Api` → `Malume.Application`, `Malume.Infrastructure`, `Malume.Shared.Contracts`
* `Malume.Web.Portal` → `Malume.Shared.Contracts`
* `Malume.Domain.UnitTests` → `Malume.Domain`
* `Malume.Application.Tests` → `Malume.Application`
* `Malume.Api.IntegrationTests` → `Malume.Api`, `Malume.Infrastructure`, `Malume.Shared.Contracts`

---

## 3. Important: Central Package Management (`NU1008`) Note

This repository uses **Central Package Management** via [Directory.Packages.props](../../Directory.Packages.props) (`ManagePackageVersionsCentrally = true`).

When Visual Studio creates a new project from a template (such as **ASP.NET Core Web API**, **Blazor WebAssembly**, or **xUnit Test Project**), the template may add `<PackageReference Include="..." Version="..." />` inside the `.csproj` file, which triggers NuGet error **`NU1008`**.

**Fix in each new `.csproj`:**
1. Remove the `Version="..."` attribute from `<PackageReference>` items in the `.csproj` (versions are already declared in `Directory.Packages.props`).
2. You can also remove redundant `<TargetFramework>`, `<Nullable>`, and `<ImplicitUsings>` from individual `.csproj` files if desired, since [Directory.Build.props](../../Directory.Build.props) already provides them—keeping `<TargetFramework>net8.0</TargetFramework>` in `.csproj` is harmless for Visual Studio UI compatibility.

---

## 4. Recommended NuGet Packages per Project

All versions are pre-pinned in [Directory.Packages.props](../../Directory.Packages.props):

* **`Malume.Domain`**: *(Keep dependency-free, or add `NetTopologySuite` only if spatial types are placed directly in Domain instead of wrapped by `GeoCoordinate`.)*
* **`Malume.Application`**: `MediatR`, `FluentValidation`, `FluentValidation.DependencyInjectionExtensions`, `NetTopologySuite`
* **`Malume.Infrastructure`**: `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.SqlServer.NetTopologySuite`, `Hangfire.Core`, `Hangfire.SqlServer`, `FirebaseAdmin`
* **`Malume.Api`**: `Microsoft.AspNetCore.Authentication.JwtBearer`, `Microsoft.AspNetCore.OpenApi`, `Microsoft.EntityFrameworkCore.Design`, `Hangfire.AspNetCore`, `Swashbuckle.AspNetCore`
* **`Malume.Web.Portal`**: `Microsoft.AspNetCore.Components.WebAssembly`, `Microsoft.AspNetCore.Components.WebAssembly.DevServer`, `Microsoft.AspNetCore.SignalR.Client`, `Microsoft.Extensions.Http`
* **Test Projects**: `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`, `coverlet.collector`, `NSubstitute`, `Shouldly` (+ `Testcontainers.MsSql` and `Microsoft.AspNetCore.Mvc.Testing` in `Malume.Api.IntegrationTests`)

---

## 5. Dockerfile Placement

Once `src/Malume.Api` is created, copy [infra/docker/api.Dockerfile.reference](../../infra/docker/api.Dockerfile.reference) to `src/Malume.Api/Dockerfile` so the GitHub Actions Docker job and local container builds match the blueprint.
