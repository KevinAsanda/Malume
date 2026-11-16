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

* Never commit `.env`, `appsettings.Development.json`, `appsettings.Local.json`, `google-services.json`, Android release keystores (`*.jks`, `*.keystore` outside RN `debug.keystore`), or Firebase service keys (`*firebase-adminsdk*.json`).
* Use `dotnet user-secrets` in `src/Malume.Api` for local SQL Server passwords, `Paystack:SecretKey` / `Paystack:PublicKey`, JWT keys, and Firebase Admin SDK credentials paths.
* Store local Android release keystores (`.jks`) inside the git-ignored `secrets/` directory.

---

## 5. CI/CD Pipeline & GitHub Repository Secrets

[`.github/workflows/ci-cd-main.yml`](../../.github/workflows/ci-cd-main.yml) runs on pushes and pull requests to `main`:

1. **`backend-verify-and-test`**: Builds `Malume.sln` (`Release`, `CI=true` treats warnings as errors) and runs Domain unit tests, Application tests, and Testcontainers SQL Server integration tests.
2. **`backend-docker-deploy`**: Builds [src/Malume.Api/Dockerfile](../../src/Malume.Api/Dockerfile) and pushes to GHCR on `main` pushes (`ghcr.io/<owner>/malume/malume-api`).
3. **`web-portal-blazor-deploy`**: Publishes `src/Malume.Web.Portal` in `Release` and uploads `malume-web-portal`.
4. **`android-driver-kotlin-apk`**: Builds `mobile/malume-driver-android` on Temurin JDK 25, runs `testDebugUnitTest` + `assembleRelease`, and uploads `malume-driver-release-apk`.
5. **`android-parent-rn-apk`**: Runs `tsc`, Jest, and `assembleRelease` on `mobile/malume-parent-rn` with Node.js 24 + Temurin JDK 17 and uploads `malume-parent-release-apk`.

### Required GitHub Actions Repository Secrets

* **Driver Android:** `DRIVER_GOOGLE_SERVICES_JSON_BASE64`, `DRIVER_KEYSTORE_BASE64`, `DRIVER_KEY_ALIAS`, `DRIVER_KEYSTORE_PASSWORD`, `DRIVER_KEY_PASSWORD`
* **Parent RN Android:** `PARENT_KEYSTORE_BASE64`, `PARENT_KEY_ALIAS`, `PARENT_KEYSTORE_PASSWORD`, `PARENT_KEY_PASSWORD` *(plus optional `PARENT_GOOGLE_SERVICES_JSON_BASE64` once Firebase is wired in Gradle)*
