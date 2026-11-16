# Malume Platform

**South African Scholar Transport & Hybrid E-Hailing Platform**

Malume combines recurring **monthly scholar transport seat subscriptions**, **1.5 km corridor-constrained peak ad-hoc rides** (and off-peak e-hailing), **B2B2C fleet & school SGB management**, **offline-first QR / daily rotating PIN child handover**, and a **40% / 30% / 30% staged milestone escrow ledger**.

---

## Architecture & Tech Stack

| Layer | Technology | Target Directory |
| :--- | :--- | :--- |
| **Backend API & SignalR Hub** | ASP.NET Core Web API (`.NET 8.0`), Clean Architecture, MediatR, FluentValidation, Hangfire | [`src/Malume.Api`](src/Malume.Api) |
| **Database & Spatial Engine** | SQL Server 2022 + EF Core + `NetTopologySuite` (`geography`, SRID 4326) | [`infra/docker/`](infra/docker), [`src/Malume.Infrastructure`](src/Malume.Infrastructure) |
| **B2B2C Web Portal** | Blazor WebAssembly (`.NET 8.0`) | [`src/Malume.Web.Portal`](src/Malume.Web.Portal) |
| **Driver Companion App** | Native Android (Kotlin, Jetpack Compose, Room + SQLCipher, CameraX + ML Kit, WorkManager) | [`mobile/malume-driver-android`](mobile/malume-driver-android) |
| **Parent Mobile App** | React Native `0.88` (TypeScript) | [`mobile/malume-parent-rn`](mobile/malume-parent-rn) |
| **Automated Testing** | xUnit, Shouldly, NSubstitute, Testcontainers (`Testcontainers.MsSql`) | [`tests/`](tests) |
| **CI/CD Pipeline** | GitHub Actions (5 jobs: .NET + Testcontainers, GHCR Docker, Blazor WASM, Signed Android APKs) | [`.github/workflows/ci-cd-main.yml`](.github/workflows/ci-cd-main.yml) |

---

## Repository Layout

```text
Malume/
├── .editorconfig
├── .gitattributes
├── .gitignore
├── .github/workflows/ci-cd-main.yml
├── Directory.Build.props                               # Shared net8.0, Nullable, Analyzers, CI=true errors
├── Directory.Packages.props                            # Central Package Management (CPM)
├── global.json                                         # Pins .NET 8.0 SDK
├── Malume.sln
├── README.md
├── malume_master_system_architecture_product_blueprint.md
├── docs/
│   ├── README.md
│   ├── adr/0001-platform-baseline-and-governance.md
│   └── setup/
│       ├── local-development.md                        # Local tooling, secrets hygiene & CI/CD secrets
│       └── visual-studio-solution-setup.md
├── infra/
│   ├── README.md
│   ├── docker/
│   │   ├── .env.example
│   │   ├── api.Dockerfile.reference
│   │   └── docker-compose.yml                          # SQL Server 2022 spatial container
│   └── sql/
│       └── init-database.sql
├── mobile/
│   ├── malume-driver-android/                          # Native Android (Kotlin + Compose)
│   └── malume-parent-rn/                               # React Native (TypeScript)
├── src/
│   ├── Malume.Api/
│   ├── Malume.Application/
│   ├── Malume.Domain/
│   ├── Malume.Infrastructure/
│   ├── Malume.Shared.Contracts/
│   └── Malume.Web.Portal/
└── tests/
    ├── Malume.Api.IntegrationTests/
    ├── Malume.Application.Tests/
    └── Malume.Domain.UnitTests/
```

---

## Getting Started

1. **Local SQL Server & CLI setup:** Follow [docs/setup/local-development.md](docs/setup/local-development.md) and [infra/README.md](infra/README.md).
2. **Visual Studio 2026:** Open `Malume.sln` and configure multiple startup projects (`Malume.Api` + `Malume.Web.Portal`).
3. **Branching workflow:** Create feature branches off `main` (e.g., `feat/phase1-domain-and-auth`) and open Pull Requests into `main` so [`.github/workflows/ci-cd-main.yml`](.github/workflows/ci-cd-main.yml) validates all 5 CI checks before merging.
4. **Architecture & implementation roadmap:** See [malume_master_system_architecture_product_blueprint.md](malume_master_system_architecture_product_blueprint.md) (Phase 1 starts with Core Domain entities, EF Core `geography` migrations, and JWT multi-tenant authentication).
