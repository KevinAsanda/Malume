# Malume Platform

**South African Scholar Transport & Hybrid E-Hailing Platform**

Malume combines recurring **monthly scholar transport seat subscriptions**, **1.5 km corridor-constrained peak ad-hoc rides** (and off-peak e-hailing), **B2B2C fleet & school SGB management**, **offline-first QR / daily rotating PIN child handover**, and a **40% / 30% / 30% staged milestone escrow ledger**.

---

## Architecture & Tech Stack

| Layer | Technology | Target Directory |
| :--- | :--- | :--- |
| **Backend API & SignalR Hub** | ASP.NET Core Web API (`.NET 8.0`), Clean Architecture, MediatR, FluentValidation, Hangfire | `src/Malume.*` *(to be created in Visual Studio)* |
| **Database & Spatial Engine** | SQL Server 2022 + EF Core + `NetTopologySuite` (`geography`, SRID 4326) | `infra/docker/`, `src/Malume.Infrastructure` |
| **B2B2C Web Portal** | Blazor WebAssembly (`.NET 8.0`) | `src/Malume.Web.Portal` |
| **Driver Companion App** | Native Android (Kotlin, Jetpack Compose, Room + SQLCipher, CameraX + ML Kit, WorkManager) | `mobile/malume-driver-android` |
| **Parent Mobile App** | React Native (TypeScript) | `mobile/malume-parent-rn` |
| **Automated Testing** | xUnit, Shouldly, NSubstitute, Testcontainers (`Testcontainers.MsSql`) | `tests/Malume.*` |

---

## Current Repository Layout

```text
Malume/
├── .editorconfig
├── .gitattributes
├── .gitignore
├── Directory.Build.props                               # Shared net8.0, Nullable, Analyzers
├── Directory.Packages.props                            # Central Package Management (CPM)
├── global.json                                         # Pins .NET 8.0 SDK (8.0.100, rollForward: latestFeature)
├── README.md
├── Malume Master System Architecture & Product Blueprint.pdf
├── malume_master_system_architecture_product_blueprint.md
├── docs/
│   ├── README.md
│   ├── adr/0001-platform-baseline-and-governance.md
│   └── setup/
│       ├── local-development.md
│       └── visual-studio-solution-setup.md             # Guide for creating Malume.sln in VS 2026
└── infra/
    ├── README.md
    ├── docker/
    │   ├── .env.example
    │   ├── api.Dockerfile.reference
    │   └── docker-compose.yml                          # SQL Server 2022 spatial container
    └── sql/
        └── init-database.sql
```

---

## Next Steps

1. Review [docs/setup/visual-studio-solution-setup.md](docs/setup/visual-studio-solution-setup.md) and create `Malume.sln` plus the `src/` and `tests/` projects in **Visual Studio 2026**.
2. Start the local SQL Server 2022 container using [infra/README.md](infra/README.md).
3. Follow the full system design in [malume_master_system_architecture_product_blueprint.md](malume_master_system_architecture_product_blueprint.md).
