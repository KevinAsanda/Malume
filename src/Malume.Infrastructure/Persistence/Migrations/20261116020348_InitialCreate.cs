using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Malume.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WalletLedgerEntries",
                columns: table => new
                {
                    LedgerEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransactionGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WalletAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntryType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    AmountZar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ReferenceType = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                    PaystackReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletLedgerEntries", x => x.LedgerEntryId);
                });

            migrationBuilder.CreateTable(
                name: "ComplianceDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DriverProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DocumentType = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                    DocumentNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StorageUrl = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsVerified = table.Column<bool>(type: "bit", nullable: false),
                    VerifiedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VerifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DailyTripInstances",
                columns: table => new
                {
                    TripId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RouteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DriverProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AvailableCorridorSeats = table.Column<int>(type: "int", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ArrivedAtSchoolGateUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyTripInstances", x => x.TripId);
                });

            migrationBuilder.CreateTable(
                name: "DriverProfiles",
                columns: table => new
                {
                    DriverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FleetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ComplianceStatus = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                    OperatingMode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PrdpNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PrdpExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PoliceClearanceExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CurrentLocation = table.Column<Point>(type: "geography", nullable: true),
                    LastLocationUpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DriverProfiles", x => x.DriverId);
                });

            migrationBuilder.CreateTable(
                name: "EscrowMilestones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrancheNumber = table.Column<int>(type: "int", nullable: false),
                    Percentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    AmountZar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ScheduledReleaseDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ReleasedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PaystackTransferCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EscrowMilestones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Learners",
                columns: table => new
                {
                    LearnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Grade = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    HomePickupPoint = table.Column<Point>(type: "geography", nullable: false),
                    HomeAddressText = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    EmergencyContactPhone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    QrSecretSalt = table.Column<byte[]>(type: "varbinary(32)", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Learners", x => x.LearnerId);
                });

            migrationBuilder.CreateTable(
                name: "TripStopManifests",
                columns: table => new
                {
                    ManifestEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TripId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LearnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BookingType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SequenceOrder = table.Column<int>(type: "int", nullable: false),
                    StopLocation = table.Column<Point>(type: "geography", nullable: false),
                    AttendanceStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    HandoverMethod = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    HandoverTimestampUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    HandoverGpsPoint = table.Column<Point>(type: "geography", nullable: true),
                    EstimatedArrivalUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    WasSyncedOffline = table.Column<bool>(type: "bit", nullable: false),
                    OfflineEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripStopManifests", x => x.ManifestEntryId);
                    table.ForeignKey(
                        name: "FK_TripStopManifests_DailyTripInstances_TripId",
                        column: x => x.TripId,
                        principalTable: "DailyTripInstances",
                        principalColumn: "TripId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TripStopManifests_Learners_LearnerId",
                        column: x => x.LearnerId,
                        principalTable: "Learners",
                        principalColumn: "LearnerId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledRoutes",
                columns: table => new
                {
                    RouteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DriverProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RouteName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CorridorPath = table.Column<LineString>(type: "geography", nullable: false),
                    MonthlyPriceZar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ActiveSubscribedSeats = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledRoutes", x => x.RouteId);
                    table.ForeignKey(
                        name: "FK_ScheduledRoutes_DriverProfiles_DriverProfileId",
                        column: x => x.DriverProfileId,
                        principalTable: "DriverProfiles",
                        principalColumn: "DriverId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SchoolEndorsements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DriverProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EndorsedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    EndorsedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolEndorsements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchoolEndorsements_DriverProfiles_DriverProfileId",
                        column: x => x.DriverProfileId,
                        principalTable: "DriverProfiles",
                        principalColumn: "DriverId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    SubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LearnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RouteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BillingYearMonth = table.Column<string>(type: "nchar(7)", fixedLength: true, maxLength: 7, nullable: false),
                    TotalAmountZar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EscrowStatus = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                    PaystackReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.SubscriptionId);
                    table.ForeignKey(
                        name: "FK_Subscriptions_Learners_LearnerId",
                        column: x => x.LearnerId,
                        principalTable: "Learners",
                        principalColumn: "LearnerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Subscriptions_ScheduledRoutes_RouteId",
                        column: x => x.RouteId,
                        principalTable: "ScheduledRoutes",
                        principalColumn: "RouteId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Tenants_Fleets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CompanyRegistrationNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PaystackSubaccountCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants_Fleets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tenants_Schools",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EmisNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    AddressText = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    SchoolGateLocation = table.Column<Point>(type: "geography", nullable: false),
                    GateGeofenceRadiusMeters = table.Column<int>(type: "int", nullable: false),
                    AdminUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants_Schools", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    TenantFleetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantSchoolId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Tenants_Fleets_TenantFleetId",
                        column: x => x.TenantFleetId,
                        principalTable: "Tenants_Fleets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Users_Tenants_Schools_TenantSchoolId",
                        column: x => x.TenantSchoolId,
                        principalTable: "Tenants_Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Vehicles",
                columns: table => new
                {
                    VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FleetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SchoolId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedDriverId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RegistrationNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    MakeModel = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    TotalSeatCapacity = table.Column<int>(type: "int", nullable: false),
                    LicenseDiscExpiryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CofExpiryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehicles", x => x.VehicleId);
                    table.ForeignKey(
                        name: "FK_Vehicles_DriverProfiles_AssignedDriverId",
                        column: x => x.AssignedDriverId,
                        principalTable: "DriverProfiles",
                        principalColumn: "DriverId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Vehicles_Tenants_Fleets_FleetId",
                        column: x => x.FleetId,
                        principalTable: "Tenants_Fleets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Vehicles_Tenants_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Tenants_Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceDocuments_DriverProfileId",
                table: "ComplianceDocuments",
                column: "DriverProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceDocuments_VehicleId",
                table: "ComplianceDocuments",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceDocuments_VerifiedByUserId",
                table: "ComplianceDocuments",
                column: "VerifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyTripInstances_DriverProfileId",
                table: "DailyTripInstances",
                column: "DriverProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyTripInstances_RouteId_ServiceDate",
                table: "DailyTripInstances",
                columns: new[] { "RouteId", "ServiceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyTripInstances_VehicleId",
                table: "DailyTripInstances",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_DriverProfiles_FleetId",
                table: "DriverProfiles",
                column: "FleetId");

            migrationBuilder.CreateIndex(
                name: "IX_DriverProfiles_UserId",
                table: "DriverProfiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EscrowMilestones_SubscriptionId_TrancheNumber",
                table: "EscrowMilestones",
                columns: new[] { "SubscriptionId", "TrancheNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Learners_ParentUserId",
                table: "Learners",
                column: "ParentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Learners_SchoolId",
                table: "Learners",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledRoutes_DriverProfileId",
                table: "ScheduledRoutes",
                column: "DriverProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledRoutes_SchoolId",
                table: "ScheduledRoutes",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledRoutes_VehicleId",
                table: "ScheduledRoutes",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolEndorsements_DriverProfileId",
                table: "SchoolEndorsements",
                column: "DriverProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolEndorsements_EndorsedByUserId",
                table: "SchoolEndorsements",
                column: "EndorsedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolEndorsements_SchoolId_DriverProfileId",
                table: "SchoolEndorsements",
                columns: new[] { "SchoolId", "DriverProfileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_LearnerId",
                table: "Subscriptions",
                column: "LearnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_ParentUserId",
                table: "Subscriptions",
                column: "ParentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_RouteId",
                table: "Subscriptions",
                column: "RouteId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Fleets_OwnerUserId",
                table: "Tenants_Fleets",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Schools_AdminUserId",
                table: "Tenants_Schools",
                column: "AdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TripStopManifests_LearnerId",
                table: "TripStopManifests",
                column: "LearnerId");

            migrationBuilder.CreateIndex(
                name: "IX_TripStopManifests_OfflineEventId",
                table: "TripStopManifests",
                column: "OfflineEventId",
                unique: true,
                filter: "[OfflineEventId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TripStopManifests_TripId",
                table: "TripStopManifests",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_NormalizedEmail",
                table: "Users",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Role",
                table: "Users",
                column: "Role");

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantFleetId",
                table: "Users",
                column: "TenantFleetId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantSchoolId",
                table: "Users",
                column: "TenantSchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_AssignedDriverId",
                table: "Vehicles",
                column: "AssignedDriverId");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_FleetId",
                table: "Vehicles",
                column: "FleetId");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_RegistrationNumber",
                table: "Vehicles",
                column: "RegistrationNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_SchoolId",
                table: "Vehicles",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletLedgerEntries_TransactionGroupId",
                table: "WalletLedgerEntries",
                column: "TransactionGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletLedgerEntries_WalletAccountId",
                table: "WalletLedgerEntries",
                column: "WalletAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_ComplianceDocuments_DriverProfiles_DriverProfileId",
                table: "ComplianceDocuments",
                column: "DriverProfileId",
                principalTable: "DriverProfiles",
                principalColumn: "DriverId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ComplianceDocuments_Users_VerifiedByUserId",
                table: "ComplianceDocuments",
                column: "VerifiedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ComplianceDocuments_Vehicles_VehicleId",
                table: "ComplianceDocuments",
                column: "VehicleId",
                principalTable: "Vehicles",
                principalColumn: "VehicleId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DailyTripInstances_DriverProfiles_DriverProfileId",
                table: "DailyTripInstances",
                column: "DriverProfileId",
                principalTable: "DriverProfiles",
                principalColumn: "DriverId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DailyTripInstances_ScheduledRoutes_RouteId",
                table: "DailyTripInstances",
                column: "RouteId",
                principalTable: "ScheduledRoutes",
                principalColumn: "RouteId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DailyTripInstances_Vehicles_VehicleId",
                table: "DailyTripInstances",
                column: "VehicleId",
                principalTable: "Vehicles",
                principalColumn: "VehicleId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DriverProfiles_Tenants_Fleets_FleetId",
                table: "DriverProfiles",
                column: "FleetId",
                principalTable: "Tenants_Fleets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DriverProfiles_Users_UserId",
                table: "DriverProfiles",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EscrowMilestones_Subscriptions_SubscriptionId",
                table: "EscrowMilestones",
                column: "SubscriptionId",
                principalTable: "Subscriptions",
                principalColumn: "SubscriptionId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Learners_Tenants_Schools_SchoolId",
                table: "Learners",
                column: "SchoolId",
                principalTable: "Tenants_Schools",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Learners_Users_ParentUserId",
                table: "Learners",
                column: "ParentUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduledRoutes_Tenants_Schools_SchoolId",
                table: "ScheduledRoutes",
                column: "SchoolId",
                principalTable: "Tenants_Schools",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduledRoutes_Vehicles_VehicleId",
                table: "ScheduledRoutes",
                column: "VehicleId",
                principalTable: "Vehicles",
                principalColumn: "VehicleId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SchoolEndorsements_Tenants_Schools_SchoolId",
                table: "SchoolEndorsements",
                column: "SchoolId",
                principalTable: "Tenants_Schools",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SchoolEndorsements_Users_EndorsedByUserId",
                table: "SchoolEndorsements",
                column: "EndorsedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Subscriptions_Users_ParentUserId",
                table: "Subscriptions",
                column: "ParentUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tenants_Fleets_Users_OwnerUserId",
                table: "Tenants_Fleets",
                column: "OwnerUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tenants_Schools_Users_AdminUserId",
                table: "Tenants_Schools",
                column: "AdminUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("CREATE SPATIAL INDEX [IX_Tenants_Schools_SchoolGateLocation] ON [dbo].[Tenants_Schools] ([SchoolGateLocation]);");
            migrationBuilder.Sql("CREATE SPATIAL INDEX [IX_Learners_HomePickupPoint] ON [dbo].[Learners] ([HomePickupPoint]);");
            migrationBuilder.Sql("CREATE SPATIAL INDEX [IX_ScheduledRoutes_CorridorPath] ON [dbo].[ScheduledRoutes] ([CorridorPath]);");
            migrationBuilder.Sql("CREATE SPATIAL INDEX [IX_TripStopManifests_StopLocation] ON [dbo].[TripStopManifests] ([StopLocation]);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX [IX_TripStopManifests_StopLocation] ON [dbo].[TripStopManifests];");
            migrationBuilder.Sql("DROP INDEX [IX_ScheduledRoutes_CorridorPath] ON [dbo].[ScheduledRoutes];");
            migrationBuilder.Sql("DROP INDEX [IX_Learners_HomePickupPoint] ON [dbo].[Learners];");
            migrationBuilder.Sql("DROP INDEX [IX_Tenants_Schools_SchoolGateLocation] ON [dbo].[Tenants_Schools];");
            migrationBuilder.DropForeignKey(
                name: "FK_Tenants_Fleets_Users_OwnerUserId",
                table: "Tenants_Fleets");

            migrationBuilder.DropForeignKey(
                name: "FK_Tenants_Schools_Users_AdminUserId",
                table: "Tenants_Schools");

            migrationBuilder.DropTable(
                name: "ComplianceDocuments");

            migrationBuilder.DropTable(
                name: "EscrowMilestones");

            migrationBuilder.DropTable(
                name: "SchoolEndorsements");

            migrationBuilder.DropTable(
                name: "TripStopManifests");

            migrationBuilder.DropTable(
                name: "WalletLedgerEntries");

            migrationBuilder.DropTable(
                name: "Subscriptions");

            migrationBuilder.DropTable(
                name: "DailyTripInstances");

            migrationBuilder.DropTable(
                name: "Learners");

            migrationBuilder.DropTable(
                name: "ScheduledRoutes");

            migrationBuilder.DropTable(
                name: "Vehicles");

            migrationBuilder.DropTable(
                name: "DriverProfiles");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Tenants_Fleets");

            migrationBuilder.DropTable(
                name: "Tenants_Schools");
        }
    }
}
