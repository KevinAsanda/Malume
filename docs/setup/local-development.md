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

---

## 9. Phase 5 Local Setup (Paystack Test Mode, Staged Escrow & GitHub Releases)

* **Paystack Test Keys (Local User Secrets Only):** Configure `Paystack:SecretKey` (`sk_test_...`) and `Paystack:PublicKey` (`pk_test_...`) in `src/Malume.Api` via `dotnet user-secrets set`. Never commit Paystack keys or webhook secrets into source files.
* **Staged Milestone Escrow (40% / 30% / 30%):**
  * Parents initialize payments via `POST /api/v1/payments/subscriptions/initialize` and verify via `POST /api/v1/payments/subscriptions/{reference}/verify` (or signed `x-paystack-signature` webhooks on `POST /api/v1/payments/paystack/webhook`).
  * Hangfire recurring job `escrow-milestone-payout` (`0 4 * * *` UTC) and `POST /api/v1/admin/jobs/escrow-payouts` evaluate due tranches (`1st` = 40%, `15th` = 30% requiring `>= 85%` trip completion, `Month-End` = 30% after verified handover), freeze unreleased tranches if compliance lapses, and record balanced double-entry pairs in `WalletLedgerEntries`.
* **Blazor Escrow & Wallet Console:** Sign in as `FleetOwner` or `SysAdmin` and open `/escrow` to configure Fleet Paystack subaccounts, inspect staged tranches, trigger payouts, or issue 1-click pro-rata refunds on paused subscriptions.
* **Tagged GitHub Releases:** Pushing a semantic version tag (`git tag v1.0.0 && git push origin v1.0.0`) runs `.github/workflows/ci-cd-main.yml` including job `6. Publish GitHub Release (APKs & Blazor Bundle)` to attach signed Driver & Parent APKs and the Blazor WebAssembly bundle.

---

## 10. Phase 6 Local Setup (SOS Dispatch Console, Interactive Maps, Parent Checkout & FCM Push)

* **SOS Dispatch & Lifecycle:** Both the Kotlin Driver app and React Native Parent app can dispatch SOS alerts on `POST /api/v1/trips/{tripId}/sos` (`SosIncidents` table + SignalR `SosIncidentCreated` broadcast to trip, school gate, and admin SOS groups). Sign in as `SysAdmin` or `SchoolAdmin` in `Malume.Web.Portal` and open `/sos` to triage, acknowledge, and resolve incidents.
* **Interactive Leaflet Maps (`Malume.Web.Portal`):** `/school/gate-board` renders live driver coordinates against the `150 m` pickup zone and `2.0 km` approaching ring, and `/sos` plots active/resolved emergency incidents on Leaflet OpenStreetMap tiles.
* **Parent App Paystack Checkout & Live Tracker:** Open the **Escrow & Pay** tab in `mobile/malume-parent-rn` to start Paystack monthly checkout, verify references, and monitor 40%/30%/30% tranches; active trip cards on **My Trips** display a live spatial tracker and **SOS Panic** trigger.
* **Firebase Cloud Messaging (Optional Locally):** Set `Firebase:ServiceAccountFilePath` or `Firebase:ServiceAccountJson` (via `dotnet user-secrets`) or `GOOGLE_APPLICATION_CREDENTIALS` in `src/Malume.Api` to dispatch live FCM notifications to registered tokens (`POST /api/v1/notifications/device-token`). When no credentials are configured, `FirebasePushNotificationService` skips network dispatch cleanly without failing requests.
