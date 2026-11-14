# ADR 0001: Platform Baseline, .NET 8 Target, and Repository Governance

* **Status:** Accepted
* **Date:** 2026-11-14

## Context

The Malume platform spans an ASP.NET Core Web API, a Blazor WebAssembly B2B2C portal, a SQL Server spatial database, a Native Android (Kotlin) Driver app, and a React Native (TypeScript) Parent app. Multiple SDKs (`.NET 8`, `10`, `11`, JDK `17` & `26`) coexist on the development machine.

## Decisions

1. **Pin .NET 8.0 via `global.json` (`8.0.100`, `rollForward: latestFeature`):**
   * Resolves to `.NET 8.0.428` locally and stays aligned with the blueprint's GitHub Actions (`dotnet-version: '8.0.x'`) workflow.
2. **Centralize MSBuild & NuGet Governance (`Directory.Build.props` & `Directory.Packages.props`):**
   * Enforces `net8.0`, C# 12, `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`, `8.0-recommended` analyzers, and Central Package Management (`ManagePackageVersionsCentrally = true`).
   * Uses permissive OSS-licensed packages (`MediatR 12.5.0` Apache-2.0, `FluentValidation 12.2.0` Apache-2.0, `Shouldly 4.3.0` BSD-3-Clause, `NSubstitute 6.2.0` BSD-3-Clause, `Testcontainers.MsSql 4.16.0` MIT).
3. **Standardize Cross-Platform Line Endings (`.gitattributes` & `.editorconfig`):**
   * Uses `LF` for source, config, containers, and shell/Gradle scripts so Windows Visual Studio edits build cleanly inside Linux Docker images and Ubuntu CI runners, while keeping `CRLF` only for Windows `.bat`/`.cmd`/`.ps1` files.
4. **SQL Server 2022 Containerized Local Persistence (`infra/docker/docker-compose.yml`):**
   * Matches production SQL Server `geography` (SRID 4326) spatial types and `Testcontainers.MsSql` integration tests without relying on LocalDB limitations for spatial/Hangfire behavior.

## Consequences

* New Visual Studio `.csproj` files must strip inline `Version="..."` attributes on `<PackageReference>` items so Central Package Management remains the single source of truth.
* Upgrading to `.NET 10` later only requires updating `global.json`, `Directory.Build.props`, and package versions in `Directory.Packages.props`.
