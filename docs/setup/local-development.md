# Local Development Setup

## 1. Prerequisites Verified on Workstation

* **Git** (`2.55+`) & **GitHub CLI (`gh`)**
* **.NET SDKs:** `8.0.428` (pinned by `global.json`), `10.0.400`, `11.0.100`, plus `dotnet-ef` global CLI
* **Visual Studio 2026** (Web, Data, Azure, Cross-Platform, Docker tools) & **SSMS 22**
* **Docker Desktop + WSL 2** (required for local SQL Server 2022 & Testcontainers)
* **Node.js** (`v24.19.0`) & **Temurin JDK 17** (`JAVA_HOME`)
* **Android Studio & Android SDK** (`ANDROID_HOME`, platforms 35/36/37, `emulator`, `adb`, `Medium_Phone` AVD)

---

## 2. Start Local SQL Server 2022 (Spatial Enabled)

```powershell
Copy-Item infra/docker/.env.example infra/docker/.env
docker compose -f infra/docker/docker-compose.yml --env-file infra/docker/.env up -d
```

See [infra/README.md](../../infra/README.md) for health checks, database initialization, and connection strings.

---

## 3. Useful CLI Commands (Once `Malume.sln` Exists)

```powershell
# Verify pinned .NET 8 SDK
dotnet --version

# Build & run all tests (requires Docker Desktop running for Testcontainers)
dotnet build Malume.sln
dotnet test Malume.sln

# Create & apply EF Core migrations (from repo root)
dotnet ef migrations add InitialCreate --project src/Malume.Infrastructure --startup-project src/Malume.Api
dotnet ef database update --project src/Malume.Infrastructure --startup-project src/Malume.Api
```

---

## 4. Secrets Hygiene

* Never commit `.env`, `appsettings.Development.json`, `appsettings.Local.json`, Android keystores (`*.jks`, `*.keystore`), or Firebase service keys (`*firebase-adminsdk*.json`).
* Use `dotnet user-secrets` in `src/Malume.Api` for local JWT keys, SQL Server passwords, and Paystack test secret keys.
