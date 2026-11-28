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

---

## 6. Phase 2 Local Setup (Compliance, Hangfire & Blazor Portal)

* **Bootstrap SysAdmin (dev/local user secrets):** Set `Bootstrap:SysAdminEmail` and `Bootstrap:SysAdminPassword` in `src/Malume.Api` via `dotnet user-secrets set` (e.g. `admin@malume.local`) so `DatabaseInitializerHostedService` seeds the initial `SysAdmin` account on startup when missing. Never store bootstrap passwords in committed `appsettings*.json` files.
* **Hangfire & Scheduled Compliance Scan:** `Malume.Api` registers Hangfire using the SQL Server `hangfire` schema and schedules `compliance-expiry-scanner` (`0 22 * * *` UTC / `00:00` SAST) to run `IComplianceExpiryScanner`. In `Development`, the Hangfire dashboard is mounted at `/hangfire`.
* **Manual Compliance Scan Endpoint:** Authenticated `SysAdmin` callers can trigger an immediate scan via `POST /api/v1/admin/jobs/compliance-scan`.
* **Blazor WASM Portal:** Sign in at `/login` with your JWT credentials to access `/admin/compliance` (`SysAdmin`), `/fleet` (`FleetOwner`), and `/school` (`SchoolAdmin`).

---

## 7. Phase 3 Local Setup (Driver Companion App, Offline Handover & SignalR)

* **Android Emulator Networking:** Run the backend with `dotnet run --project src/Malume.Api --launch-profile http` (`http://localhost:5144`). The Kotlin Driver app in `mobile/malume-driver-android` defaults to `http://10.0.2.2:5144` (configured for cleartext loopback/emulator traffic in `network_security_config.xml`).
* **Persistent ECDSA QR Signing Key (Optional for Local Dev):** By default, `EcdsaQrBadgeSigner` generates an in-memory P-256 keypair per API startup. To keep previously generated QR badges valid across API restarts locally, set `QrSigning:PrivateKeyBase64` (PKCS#8 Base64) via `dotnet user-secrets` in `src/Malume.Api`.
* **SignalR Live Telemetry Hub:** Real-time GPS telemetry, school-gate approach events, and SOS alerts run on `/hubs/trips` (JWT Bearer token via header or `?access_token=` query string).
* **Offline Handover & Airplane Mode Testing:**
  * Refresh the Driver schedule while online so the manifest (`QrPublicKeyVerifier` and `DailyPinVerifierHash`) is cached in the SQLCipher-encrypted Room database.
  * Toggle **Airplane Mode** on the emulator/device and verify learners using **Scan QR** (CameraX/ML Kit or manual `MLM1:...` payload entry) or **Daily PIN** (4-digit code from `GET /api/v1/learners/{id}/daily-pin`).
  * Turn connectivity back on: `ConnectivitySyncMonitor` + `HandoverSyncWorker` (WorkManager) automatically flush queued `PENDING` records in chronological order to `POST /api/v1/trips/{tripId}/handovers/batch-sync`.

---

## 8. Phase 4 Local Setup (Parent RN App, Spatial Corridor & Gate Board)

* **React Native Parent App (`mobile/malume-parent-rn`):** Run Metro and Android (`npm start`, `npm run android`) with `Malume.Api` running on `--launch-profile http` (`http://localhost:5144`, accessed from the Android Emulator at `http://10.0.2.2:5144` via `network_security_config.xml`).
* **1.5 km Spatial Corridor & Off-Peak E-Hailing:** Parents can register learners, view signed ECDSA QR payloads + Today's 4-digit Daily PIN, search active trips within `1.5 km` (`GET /api/v1/corridor/options`) using SQL Server `geography::STDistance`, book empty seats (`POST /api/v1/corridor/trips/{tripId}/book`), or search compliant off-peak drivers within `5 km` (`GET /api/v1/corridor/off-peak-drivers`).
* **Mark Absent Today & Dynamic ETA Cascade:** Tapping **Mark Absent Today** calls `POST /api/v1/trips/{tripId}/learners/{learnerId}/mark-absent`, increments `AvailableCorridorSeats` by `1`, recomputes downstream stop ETAs, and pushes `ManifestMutatedEvent` + `EtaCascadeUpdated` over SignalR `/hubs/trips` when downstream stops shift `>= 2 minutes` earlier.
* **Live Afternoon Gate-Arrival Board:** `SchoolAdmin` and `SysAdmin` users can open `/school/gate-board` in `Malume.Web.Portal` to monitor **Approaching (`< 2.0 km`)**, **At Pickup Zone (`< 150 m`)** boarding progress, **Departed** runs, and SOS banners in real time.
