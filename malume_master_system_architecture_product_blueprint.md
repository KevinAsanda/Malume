# MALUME — Master System Architecture & Product Blueprint
**South African Scholar Transport & Hybrid E-Hailing Platform**

* **Platform Name:** Malume
* **Architecture Pattern:** Clean Architecture + Spatial Event-Driven Telemetry + Offline-First Sync
* **Core Tech Stack:** ASP.NET Core Web API (.NET 8/9), Native Android (Kotlin + Jetpack Compose), React Native (TypeScript), Blazor WebAssembly (.NET 8/9), SQL Server + EF Core (`NetTopologySuite`), SignalR, Paystack SA, Docker & GitHub Actions CI/CD
* **Regulatory Alignment:** South African National Land Transport Amendment Act (NLTA)

---

## 1. Executive Summary & Product Vision

In South Africa, hundreds of thousands of learners commute daily via informal private scholar transport operators affectionately known as *"Malume"*. Despite its essential role in education access, the industry remains manual and fragmented:
* Parents discover drivers via word-of-mouth, coordinate daily absences on noisy WhatsApp groups, and have zero real-time visibility into whether their child has safely boarded the vehicle or arrived at the school gate.
* Parents pay upfront monthly fees via cash or manual EFT on the 1st of the month with no escrow protection if a vehicle breaks down mid-month, while drivers struggle with fuel cash-flow and unverified schedules.
* Schools and authorities lack a centralized mechanism to verify driver Professional Driving Permits (PrDPs), Certificate of Fitness (COF) roadworthy certificates, and SAPS police clearances.

**Malume** solves these problems by combining **monthly subscription scholar transport**, **corridor-constrained peak ad-hoc rides**, and **B2B2C fleet and school management** into a single, cryptographically verifiable mobility and fintech ecosystem.

### Portfolio Strategic Objective
Every component in Malume is deliberately engineered to showcase production-grade proficiency across four high-demand software engineering tracks:
1. **C# / .NET Backend Engineering:** Clean Architecture, CQRS with MediatR, Entity Framework Core with `NetTopologySuite` spatial indexing, SignalR real-time telemetry hubs, Hangfire background state machines, and a double-entry Staged Escrow Wallet Ledger.
2. **Android Mobile Engineering (Native & Cross-Platform):** A **Native Android (Kotlin + Jetpack Compose)** Driver Companion App featuring persistent Foreground Location Services, CameraX + ML Kit QR scanning, and an Offline-First Room SQLite Sync Engine for load-shedding dead zones—paired with a **React Native (TypeScript)** Parent App.
3. **Full-Stack Web Engineering:** A multi-tenant **Blazor WebAssembly (.NET 8/9)** portal for System Admins, Fleet Owners, and School Governing Bodies (SGBs) sharing compile-time C# contracts (`Malume.Shared.Contracts`) with the backend API.
4. **DevOps & CI/CD Engineering:** Multi-stage **GitHub Actions** pipelines running automated xUnit + **Testcontainers** (real SQL Server spatial containers), building optimized Docker images, deploying the Blazor WASM portal, and automatically compiling/signing release Android APKs for both mobile apps on every merge to `main`.

---

## 2. Business Models & Multi-Tenant Ecosystem

Malume unifies three operational pillars inside one cohesive domain model:

| Operating Pillar | Core Mechanics & Booking Flow | Key Engineering & Domain Rules |
| :--- | :--- | :--- |
| **1. Monthly Contract Marketplace** | Parents search verified routes running between their home suburb and their child's school, reserve a dedicated seat on morning and/or afternoon runs, and pay a monthly recurring subscription. | Seat capacity is hard-locked per vehicle (e.g., 15-seater Toyota Quantum). Monthly fees are held in a **Staged Milestone Escrow Ledger (40% / 30% / 30%)** rather than released in an unprotected lump sum. |
| **2. Hybrid Ad-Hoc E-Hailing (Rule A)** | Parents book once-off or emergency rides for missed buses, extra lessons, or afternoon sports fixtures. | **Peak Hours (`06:00–08:00` & `13:30–15:30` SAST):** Ad-hoc rides can *only* book empty seats on an active scheduled route if the pickup lies within a **1.5 km spatial corridor** of that route.<br>**Off-Peak Hours:** Drivers can toggle into unrestricted point-to-point Uber-style e-hailing mode. |
| **3. Fleet Owner & School B2B2C** | **Fleet Owners** register multiple vehicles, hire drivers, assign vehicles to routes, and receive consolidated escrow payouts.<br>**Schools (SGB / Admin)** endorse trusted operators, monitor a live afternoon gate-arrival board, and manage school-owned buses. | Tenant-scoped Role-Based Access Control (RBAC). Employed drivers authenticate into the Driver App for navigation and QR handover but cannot alter fleet payout bank accounts. |

---

## 3. End-to-End Technology Stack & Surface Mapping

| System Layer | Selected Technology | Architectural Responsibility & Portfolio Justification |
| :--- | :--- | :--- |
| **Parent Mobile App** | **React Native (TypeScript)**<br>Compiled to Android APK/AAB | Route search, monthly seat subscriptions, ad-hoc corridor/off-peak ride hailing, live SignalR map tracking, **"Mark Absent Today"** trigger, **4-digit Daily Rotating Fallback PIN** display, and Paystack checkout. |
| **Driver Companion App** | **Native Android (Kotlin)**<br>Jetpack Compose, Coroutines/Flow, Room SQLite (SQLCipher), CameraX + ML Kit, WorkManager | Persistent **Foreground Service** for continuous GPS streaming, offline-first encrypted manifest caching, `<300ms` CameraX QR scanning, offline TOTP PIN verification, dynamic waypoint sequence, and hardware-backed SOS Panic button. |
| **Admin, Fleet & School Web Portal** | **Blazor WebAssembly (.NET 8/9)**<br>C#, SignalR Client, Mapbox/Leaflet JS Interop | Multi-tenant web dashboard: System Admins verify NLTA compliance documents; Fleet Owners manage vehicles, drivers, and escrow statements; School Admins issue SGB Endorsement Badges, view the **Live Gate-Arrival Board**, and manage school buses. |
| **Core Backend API & Real-Time Hub** | **ASP.NET Core Web API (.NET 8/9)**<br>Clean Architecture, SignalR, MediatR, FluentValidation, Hangfire | RESTful APIs, real-time WebSocket telemetry ingestion (`TelemetryHub`), geospatial route optimization, staged escrow release jobs, NLTA document expiry state machines, and Firebase Cloud Messaging (FCM) push dispatch. |
| **Database & Spatial Engine** | **Microsoft SQL Server**<br>EF Core + `NetTopologySuite` (`geography`) | ACID relational persistence for the double-entry financial ledger, paired with native SQL Server `geography` spatial indexing (`LineString`, `Point`, `STDistance`) for 1.5 km corridor matching and school gate geofencing. |
| **Payments & Escrow Gateway** | **Paystack South Africa**<br>Tokenized Cards, Subaccounts, Transfers API | Processes ZAR recurring card subscriptions and Instant EFTs, splits platform commission via subaccounts, and executes automated milestone payouts (1st, 15th, and month-end) via webhook-verified ledger entries. |
| **DevOps & CI/CD** | **GitHub Actions + Docker**<br>xUnit, Testcontainers, Gradle | Automated build, test (spinning up real SQL Server containers via Testcontainers), Docker image publishing for .NET API, Blazor WASM deployment, and signed Android APK/AAB generation on every merge to `main`. |

---

## 4. Clean Architecture Solution Structure (`.NET 8/9`)

The repository follows strict Clean Architecture dependency rules: `Malume.Domain` has zero external dependencies; `Malume.Application` depends only on `Malume.Domain`; `Malume.Infrastructure` implements interfaces defined in `Application`; and `Malume.Shared.Contracts` shares DTOs and SignalR contracts between `Malume.Api` and `Malume.Web.Portal`.

```text
malume-platform/
├── .github/
│   └── workflows/
│       └── ci-cd-main.yml              # Multi-stage Test, Docker, Blazor & Signed APK Pipeline
├── src/
│   ├── Malume.Domain/                  # Enterprise Entities, Value Objects, Domain Events, Enums
│   │   ├── Entities/                   # User, Learner, DriverProfile, Vehicle, Fleet, School, Route, Trip, LedgerEntry
│   │   ├── ValueObjects/               # GeoCoordinate, ZarMoney, QrSignaturePayload, DailyRotatingPin
│   │   └── StateMachines/              # DriverComplianceState, TripLifecycleState, EscrowMilestoneState
│   ├── Malume.Application/             # CQRS Use Cases (MediatR), Interfaces, Validators (FluentValidation)
│   │   ├── Features/Subscriptions/     # CreateSubscriptionCommand, MarkLearnerAbsentCommand
│   │   ├── Features/AdHocRides/        # QueryCorridorSeatsQuery, RequestOffPeakRideCommand
│   │   ├── Features/Handover/          # ProcessOfflineSyncBatchCommand, VerifyFallbackPinCommand
│   │   └── Features/Escrow/            # ExecuteMilestoneReleaseJob, FreezeEscrowOnSuspensionHandler
│   ├── Malume.Infrastructure/          # EF Core SQL Server + NetTopologySuite, Paystack SDK, FCM, Hangfire
│   │   ├── Persistence/                # MalumeDbContext, SpatialConfigurations, Migrations
│   │   ├── Services/                   # PaystackTransferService, RouteOptimizationService, HmacQrSigningService
│   │   └── BackgroundJobs/             # ComplianceExpiryScannerJob, EscrowMilestonePayoutJob
│   ├── Malume.Shared.Contracts/        # Shared C# DTOs, SignalR Hub Interfaces, Validation Rules
│   ├── Malume.Api/                     # ASP.NET Core Web API Controllers, SignalR Hubs, JWT Middleware
│   └── Malume.Web.Portal/              # Blazor WebAssembly Portal (System Admin, Fleet Owner, School SGB)
├── mobile/
│   ├── malume-parent-rn/               # React Native (TypeScript) Parent Application
│   └── malume-driver-android/          # Native Android (Kotlin + Jetpack Compose) Driver Companion App
└── tests/
    ├── Malume.Domain.UnitTests/        # Pure domain logic, escrow math, PIN derivation, state machine tests
    ├── Malume.Application.Tests/       # CQRS handler unit tests
    └── Malume.Api.IntegrationTests/    # End-to-end API + SQL Server Spatial tests via Testcontainers
```

---

## 5. Database Schema & Spatial Data Model

The SQL Server schema uses `NetTopologySuite` `geography` types (SRID 4326) with spatial indexes on `ScheduledRoutes.CorridorPath` and `TripStopManifests.StopLocation`.

### Core Entities & Relationships

1. **`Tenants_Fleets` & `Tenants_Schools`**
   * **Fields:** `Id (PK)`, `Name`, `PaystackSubaccountCode`, `SchoolGateLocation (geography Point)`, `GateGeofenceRadiusMeters (int, default 150)`
   * **Role:** Multi-tenant root entities. Fleet Owners own `Vehicles` and employ `DriverProfiles`. Schools manage `SchoolEndorsements` and school-owned `Vehicles`.

2. **`DriverProfiles` & `ComplianceDocuments`**
   * **Fields:** `DriverId (PK)`, `UserId (FK)`, `FleetId (FK nullable)`, `ComplianceStatus (PendingReview | Active_Compliant | Active_ExpiringSoon | Suspended_ExpiredDoc | Suspended_SafetyIncident)`, `OperatingMode (FixedRoute | OffPeakAdHoc)`, `PrdpNumber`, `PrdpExpiryDate`, `PoliceClearanceExpiryDate`
   * **Role:** Tracks independent (`FleetId = NULL`) and fleet-employed drivers.

3. **`Vehicles`**
   * **Fields:** `VehicleId (PK)`, `FleetId (FK nullable)`, `SchoolId (FK nullable)`, `RegistrationNumber`, `MakeModel`, `TotalSeatCapacity (int)`, `LicenseDiscExpiryDate`, `CofExpiryDate`, `AssignedDriverId (FK)`
   * **Role:** Enforces physical seat constraints (`TotalSeatCapacity`, e.g., 15 seats) and roadworthiness dates.

4. **`Learners`**
   * **Fields:** `LearnerId (PK)`, `ParentUserId (FK)`, `SchoolId (FK)`, `FullName`, `Grade`, `HomePickupPoint (geography Point)`, `HomeAddressText`, `QrSecretSalt (byte[32])`, `EmergencyContactPhone`
   * **Role:** Stores the learner's spatial pickup point and the cryptographic salt used for both ECDSA/HMAC QR badges and the 4-digit daily rotating fallback PIN.

5. **`ScheduledRoutes`**
   * **Fields:** `RouteId (PK)`, `VehicleId (FK)`, `SchoolId (FK)`, `RouteName`, `Direction (MorningToSchool | AfternoonFromSchool)`, `CorridorPath (geography LineString)`, `MonthlyPriceZar (decimal(18,2))`, `ActiveSubscribedSeats (int)`
   * **Role:** Represents a recurring morning or afternoon route corridor. `CorridorPath` is indexed with a SQL Server Spatial Index for fast `STDistance` queries.

6. **`DailyTripInstances` & `TripStopManifests`**
   * **Fields (`DailyTripInstances`):** `TripId (PK)`, `RouteId (FK)`, `DriverId (FK)`, `VehicleId (FK)`, `ServiceDate (date)`, `Status (Scheduled | EnRoute | AtSchoolGate | Completed | Cancelled)`, `AvailableCorridorSeats (int)`
   * **Fields (`TripStopManifests`):** `ManifestEntryId (PK)`, `TripId (FK)`, `LearnerId (FK)`, `BookingType (MonthlyContract | AdHocCorridor)`, `SequenceOrder (int)`, `StopLocation (geography Point)`, `AttendanceStatus (Scheduled | MarkedAbsent | PickedUp | DroppedOff)`, `HandoverMethod (None | QrScan | FallbackPin)`, `HandoverTimestampUtc`, `HandoverGpsPoint (geography Point)`, `EstimatedArrivalUtc`, `WasSyncedOffline (bit)`

7. **`Subscriptions` & `EscrowMilestones`**
   * **Fields:** `SubscriptionId (PK)`, `ParentUserId (FK)`, `LearnerId (FK)`, `RouteId (FK)`, `BillingYearMonth (char(7))`, `TotalAmountZar`, `EscrowStatus (Active | Paused_ComplianceOrServiceFailure | Completed | Disputed)`, `Milestone1_40Pct_Status`, `Milestone2_30Pct_Status`, `Milestone3_30Pct_Status`

8. **`WalletLedgerEntries` (Immutable Double-Entry Ledger)**
   * **Fields:** `LedgerEntryId (PK)`, `TransactionGroupId (Guid)`, `WalletAccountId (FK)`, `EntryType (Debit | Credit)`, `AmountZar (decimal(18,2))`, `ReferenceType (SubscriptionCharge | MilestoneRelease_40 | MilestoneRelease_30_Mid | MilestoneRelease_30_End | AdHocRidePayout | ProRataRefund)`, `PaystackReference`, `CreatedAtUtc`

---

## 6. Core Engineering Workflows & Edge-Case State Machines

### 6.1 Workflow A: Hybrid Ad-Hoc E-Hailing & The 1.5 km Spatial Corridor Rule
Mixing monthly scholar contracts with on-demand Uber-style rides creates a critical capacity and punctuality conflict during peak hours. Malume solves this via **Rule A (Corridor Seat Fill-In + Off-Peak On-Demand)**:

1. **Peak Window (`06:00–08:00` and `13:30–15:30` SAST):**
   * Drivers cannot accept arbitrary point-to-point rides that would detour their contract route.
   * When a parent requests an urgent once-off morning or afternoon school ride, `QueryCorridorSeatsQueryHandler` runs a spatial query in SQL Server using `NetTopologySuite`:
   ```csharp
   var eligibleTrips = await _dbContext.DailyTripInstances
       .Include(t => t.Route)
           .ThenInclude(r => r.Vehicle)
       .Include(t => t.Route.School)
       .Where(t => t.ServiceDate == request.ServiceDate
                && t.Route.SchoolId == request.TargetSchoolId
                && t.AvailableCorridorSeats > 0
                && t.Route.CorridorPath.STDistance(request.PickupPoint) <= 1500.0)
       .OrderBy(t => t.Route.CorridorPath.STDistance(request.PickupPoint))
       .Select(t => new CorridorAdHocOptionDto(
           t.TripId,
           t.Driver.FullName,
           t.Route.Vehicle.MakeModel,
           t.AvailableCorridorSeats,
           t.Route.CorridorPath.STDistance(request.PickupPoint),
           t.Route.School.HasSgbEndorsement(t.DriverId)))
       .ToListAsync(ct);
   ```
   * When booked, the learner is dynamically inserted into `TripStopManifests` at the optimal `SequenceOrder` along the `LineString`, and `AvailableCorridorSeats` is decremented atomically.
2. **Off-Peak Window (Outside `06:00–08:00` and `13:30–15:30` SAST):**
   * Verified drivers whose vehicles pass NLTA compliance checks can toggle their status in the Kotlin Driver App to `OperatingMode.OffPeakAdHoc`.
   * Parents can request point-to-point rides for after-school sports, extra tuition, or weekend school events using standard proximity matching (`DriverCurrentLocation.STDistance(PickupPoint) <= 5000.0`).

---

### 6.2 Workflow B: Dynamic Route Recalculation on "Mark Absent Today"
When a parent taps **"Mark Absent Today"** at 05:45 AM (or while the driver is en route):

1. **Atomic State Transition & Seat Release:**
   * `MarkLearnerAbsentCommand` updates the learner's `TripStopManifest` entry to `AttendanceStatus.MarkedAbsent`.
   * The trip's `AvailableCorridorSeats` is incremented by `1` for that run (allowing another parent along the 1.5 km corridor to book that empty seat on an ad-hoc basis).
2. **Waypoint Pruning & ETA Re-Computation:**
   * `RouteOptimizationService` removes the absent learner's coordinate from the active stop sequence and recalculates the travel time matrix between all remaining stops on the manifest.
3. **Real-Time Driver Queue Update:**
   * SignalR pushes a `ManifestMutatedEvent` to the driver's Kotlin app, updating the Room SQLite manifest and triggering an in-app voice prompt (*"Stop skipped: Thabo Mokoena marked absent today. Routing directly to next stop."*).
4. **Downstream Parent ETA Cascade Notification:**
   * Skipping a house shifts the driver's arrival time earlier for every subsequent learner on the route.
   * The backend computes the delta (`OldEta - NewEta`) for all downstream stops; if the shift is `>= 2 minutes`, it dispatches targeted FCM push notifications and SignalR updates to those parents:
     > *"Updated ETA: A stop ahead of you was skipped today. Malume Sipho will now arrive at your gate around 06:33 AM (6 minutes earlier than scheduled)."*

---

### 6.3 Workflow C: Offline-First Child Handover Security (Load-Shedding Resilience)
Because cellular towers in South Africa frequently lose power or backhaul connectivity during rotational load-shedding, child handover verification must work **100% offline** while preventing spoofing:

1. **Pre-Trip Encrypted Manifest Caching:**
   * Whenever the Kotlin Driver App has connectivity prior to a shift, it fetches `GET /api/v1/trips/{tripId}/manifest` and stores the signed manifest in a **SQLCipher-encrypted Room database**.
   * Each manifest row contains: `LearnerId`, `LearnerName`, `PhotoThumbnailBase64`, `ExpectedStopPoint`, `QrPublicKeyVerifier`, and the learner's derived daily `DailyPinVerifierHash`.
2. **Primary Handover — Cryptographic QR Code Scan:**
   * Each learner has a durable QR badge attached to their school bag containing a compact signed payload: `MLM1:{LearnerId}:{SchoolId}:{Base64EcdsaSignature}`.
   * The Driver App uses **CameraX + Google ML Kit Barcode Scanning** to read the QR code and validates the ECDSA signature locally against the cached key in `<50ms` with zero network calls.
3. **Fallback Handover — 4-Digit Daily Rotating PIN:**
   * If a child forgets or loses their QR card, the driver taps **"Learner Forgot QR Card — Enter Daily PIN"**.
   * Both the React Native Parent App and the backend/Driver App derive a deterministic 4-digit PIN using `HMAC-SHA256`:
     ```text
     InputMessage = "{LearnerId}:{ServiceDate_yyyyMMdd}:{TripDirection}"
     Digest       = HMAC_SHA256(key: LearnerQrSecretSalt, message: InputMessage)
     PinCode      = (DynamicTruncate(Digest) % 9000) + 1000  // Always a 4-digit code (1000–9999)
     ```
   * The parent views today's 4-digit PIN inside the Parent App and gives it to their child or reads it over a regular voice call to the driver. The Driver App hashes the entered 4 digits locally and verifies it against `DailyPinVerifierHash` offline.
4. **Offline Outbox Queue & Automatic Reconciliation:**
   * Every verified pickup or drop-off writes an immutable `OfflineHandoverRecord` into Room (`EventId`, `TripId`, `LearnerId`, `Action: PickedUp | DroppedOff`, `Method: QrScan | FallbackPin`, `RecordedAtUtc`, `Lat`, `Lng`, `SyncState = PENDING`).
   * The UI immediately updates for the driver so they can proceed to the next house.
   * The moment network connectivity returns (monitored via Android `ConnectivityManager.NetworkCallback` and **WorkManager**), the app flushes all `PENDING` records in chronological order to `POST /api/v1/trips/{tripId}/handovers/batch-sync`.
   * The API idempotently applies the events (deduplicating by `EventId`) and sends a timestamp-accurate push notification to the parent: *"Amahle was safely picked up at 06:41 AM (Verified via QR Scan — synced after signal restoration)."*

---

### 6.4 Workflow D: Staged Milestone Escrow & Double-Entry Wallet Ledger
To protect parents from mid-month service abandonment while giving drivers working capital for fuel on the 1st of the month, Malume implements **Option 1 (Milestone / Staged Escrow)**:

```text
Parent Pays R1,200 on 1st via Paystack
        │
        ▼
[Parent Escrow Liability Account: +R1,200.00]
        │
        ├──► Tranche 1 (1st of Month — 40% = R480.00):
        │    Released immediately upon active route verification for early-month fuel.
        │
        ├──► Tranche 2 (15th of Month — 30% = R360.00):
        │    Released by Hangfire job ONLY IF:
        │      1. Driver & Vehicle ComplianceStatus == Active_Compliant
        │      2. CompletedTripsRatio (1st–15th) >= 85%
        │      3. EscrowStatus != Paused_ComplianceOrServiceFailure
        │
        └──► Tranche 3 (Last School Day of Month — 30% = R360.00):
             Released automatically after verified QR/PIN drop-off on final afternoon run.
```

* **Fleet Owner vs. Independent Driver Routing:** If the vehicle belongs to a Fleet Owner (`Vehicle.FleetId != null`), Paystack Transfers route the tranche payout directly to the **Fleet Owner's verified bank account / Paystack Subaccount**, while generating a per-vehicle earnings statement on the Fleet Owner's Blazor dashboard. If the driver is an independent owner-operator, funds route to the driver's bank account.
* **Automatic Escrow Freeze:** If a driver is suspended mid-month due to an expired PrDP/license disc or prolonged vehicle breakdown (and the Fleet Owner does not assign a replacement compliant vehicle within 24 hours), `FreezeEscrowOnSuspensionHandler` locks all unreleased tranches and enables 1-click pro-rata parent refunds or emergency driver re-allocation on the Admin Portal.

---

### 6.5 Workflow E: SA NLTA Regulatory Compliance State Machine & SOS Dispatch
In accordance with the **National Land Transport Amendment Act (NLTA)**:

1. **Document Expiry State Machine:**
   ```text
   [PendingReview] ──(Admin Approves PrDP + COF + Disc + SAPS Clearance)──► [Active_Compliant]
                                                                                   │
         ┌────────────────────(<= 30 Days to Earliest Expiry)──────────────────────┘
         ▼
   [Active_ExpiringSoon] ──(Driver/Fleet Uploads Renewed Doc & Admin Approves)──► [Active_Compliant]
         │
         └──(Expiry Date Reached at 00:00 SAST)──► [Suspended_ExpiredDoc]
                                                     • Blocks trip start in Kotlin Driver App
                                                     • Pauses 15th / Month-End Escrow Tranches
                                                     • Notifies Fleet Owner, School & Subscribed Parents
   ```
2. **In-App SOS / Panic Button:**
   * Persistent red SOS button on both the Kotlin Driver App and React Native Parent App during active trips.
   * Triggering SOS immediately switches the Driver App's GPS telemetry stream to `1 Hz` (every 1 second), fires a high-priority `SosIncidentCreated` event to the Blazor Admin & School Gate Portal, and dispatches emergency SMS/Push deep links containing the vehicle registration, live coordinates, and active learner manifest to emergency contacts and security partners.

---

## 7. B2B2C Web Portal Specification (Blazor WebAssembly)

The Blazor WebAssembly application (`Malume.Web.Portal`) uses role-based layout views backed by `Malume.Shared.Contracts`:

### 7.1 Fleet Owner Dashboard
* **Fleet & Vehicle Roster:** Register multiple vehicles (e.g., 5 Toyota Quantums), upload COF and License Disc renewals, and track per-vehicle seat utilization (`14/15 seats subscribed`).
* **Driver Assignment & Breakdown Swap:** Assign employed drivers to specific vehicles and routes. If Vehicle 1 has a mechanical issue at 05:30 AM, the Fleet Owner can perform an **Instant Vehicle Swap** to a spare compliant vehicle in their fleet, keeping the route active and preventing an escrow freeze.
* **Consolidated Financial Ledger:** View all 40% / 30% / 30% escrow tranches across all vehicles, download monthly tax/payout statements, and manage corporate Paystack payout details.

### 7.2 School & SGB Verification Portal
* **SGB Trusted Operator Endorsement:** School Governing Bodies (SGBs) and School Admins can review independent drivers and fleets serving their school and grant an official **SGB Endorsed Badge** (displayed prominently to parents searching for routes).
* **Live Afternoon Gate-Arrival Board:** A real-time SignalR dashboard designed to be displayed on a monitor or tablet at the school pickup zone between 13:30 and 15:30. It automatically categorizes approaching scholar transport vehicles via geofencing:
  * **Approaching (`< 2.0 km` from School Gate):** Lists vehicle registration, driver name, ETA, and the names/grades of learners on that afternoon's manifest so teachers can line them up.
  * **At Pickup Zone (`< 150 m` Geofence):** Highlights vehicles currently loading learners at the gate and updates live as the driver scans each child's QR code (`11/14 Boarded`).
  * **Departed:** Logs the exact gate departure timestamp once all manifested learners are scanned aboard.
* **School-Owned Bus Management:** Schools that own their own buses can manage their internal drivers, assign learners to school bus routes, and use the exact same Kotlin Driver App and Parent App tracking ecosystem.

---

## 8. DevOps, Testing & GitHub Actions CI/CD Pipeline

The `.github/workflows/ci-cd-main.yml` workflow executes five automated jobs on every push/merge to `main`:

```yaml
name: Malume Full-Stack & Mobile CI/CD Pipeline

on:
  push:
    branches: [ "main" ]
  pull_request:
    branches: [ "main" ]

jobs:
  backend-verify-and-test:
    name: 1. .NET 8 Clean Architecture & Spatial Integration Tests
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - name: Restore & Build Solution
        run: dotnet build Malume.sln --configuration Release
      - name: Run Domain & Application Unit Tests
        run: dotnet test tests/Malume.Domain.UnitTests --no-build --configuration Release
      - name: Run Spatial Integration Tests (Testcontainers SQL Server)
        run: dotnet test tests/Malume.Api.IntegrationTests --no-build --configuration Release

  backend-docker-deploy:
    name: 2. Build & Push ASP.NET Core API Docker Container
    needs: backend-verify-and-test
    if: github.ref == 'refs/heads/main'
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Build & Push Docker Image to GHCR
        run: |
          docker build -t ghcr.io/${{ github.repository }}/malume-api:${{ github.sha }} -f src/Malume.Api/Dockerfile .

  web-portal-blazor-deploy:
    name: 3. Publish Blazor WASM Admin/Fleet/School Portal
    needs: backend-verify-and-test
    if: github.ref == 'refs/heads/main'
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - name: Publish Blazor WebAssembly Release Bundle
        run: dotnet publish src/Malume.Web.Portal/Malume.Web.Portal.csproj -c Release -o release-web

  android-driver-kotlin-apk:
    name: 4. Build & Sign Native Android (Kotlin) Driver APK
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-java@v4
        with:
          distribution: 'temurin'
          java-version: '17'
      - name: Run Kotlin Unit Tests & Assemble Signed Release APK
        working-directory: mobile/malume-driver-android
        run: |
          ./gradlew testDebugUnitTest
          ./gradlew assembleRelease

  android-parent-rn-apk:
    name: 5. Build & Sign React Native (TypeScript) Parent APK
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: '20'
      - uses: actions/setup-java@v4
        with:
          distribution: 'temurin'
          java-version: '17'
      - name: Typecheck, Jest Test & Assemble Signed Parent Release APK
        working-directory: mobile/malume-parent-rn
        run: |
          npm ci
          npx tsc --noEmit
          npm test -- --watchAll=false
          cd android && ./gradlew assembleRelease
```

---

## 9. Phased Implementation Roadmap

Build the project in **5 vertical slices** so that every milestone produces a working, testable feature set:

* **Phase 1: Core Domain, Clean Architecture & Spatial Database (Weeks 1–2)**
  * Set up `Malume.sln` (`Domain`, `Application`, `Infrastructure`, `Api`, `Shared.Contracts`, `Web.Portal`).
  * Configure SQL Server + EF Core with `NetTopologySuite` (`geography` SRID 4326) and build the Testcontainers integration test suite.
  * Implement JWT authentication and multi-tenant role claims (`Parent`, `Driver`, `FleetOwner`, `SchoolAdmin`, `SysAdmin`).
* **Phase 2: NLTA Compliance State Machine & Blazor B2B2C Web Portal (Weeks 3–4)**
  * Build the Blazor WebAssembly portal for Fleet Owners, School SGB Admins, and System Admins.
  * Implement driver/vehicle registration, PrDP/COF expiry tracking, Hangfire's `ComplianceExpiryScannerJob`, and School SGB Endorsement Badges.
* **Phase 3: Native Kotlin Driver Companion App & Offline Handover Engine (Weeks 5–6)**
  * Scaffold `malume-driver-android` using Kotlin, Jetpack Compose, Room SQLite (SQLCipher), CameraX + ML Kit, and WorkManager.
  * Implement encrypted manifest caching, offline QR verification, the deterministic 4-digit Daily Rotating Fallback PIN validator, the offline sync outbox, and the Foreground GPS Service streaming to SignalR.
* **Phase 4: React Native Parent App, Corridor E-Hailing & Dynamic ETAs (Weeks 7–8)**
  * Scaffold `malume-parent-rn` using React Native and TypeScript.
  * Build route discovery, the **1.5 km spatial corridor ad-hoc seat filter**, **"Mark Absent Today"** dynamic route recalculation + ETA cascade notifications, live SignalR driver map tracking, and the Blazor **Live Afternoon Gate-Arrival Board**.
* **Phase 5: Paystack Staged Escrow Ledger & CI/CD Release Automation (Weeks 9–10)**
  * Implement the double-entry `WalletLedgerEntries` engine, Paystack card tokenization/subaccounts, and the **40% (1st) / 30% (15th) / 30% (Month-End)** Hangfire milestone payout jobs with automatic suspension freezes.
  * Configure the GitHub Actions pipeline to automatically run tests, publish Docker images, deploy the Blazor portal, and sign/attach both Android APKs to GitHub Releases.