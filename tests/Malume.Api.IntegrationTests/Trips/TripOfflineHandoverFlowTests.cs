using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Malume.Api.IntegrationTests.Infrastructure;
using Malume.Domain.Common;
using Malume.Domain.Enums;
using Malume.Shared.Contracts.Auth;
using Malume.Shared.Contracts.Compliance;
using Malume.Shared.Contracts.Drivers;
using Malume.Shared.Contracts.Learners;
using Malume.Shared.Contracts.Routes;
using Malume.Shared.Contracts.Tenants;
using Malume.Shared.Contracts.Trips;
using Malume.Shared.Contracts.Vehicles;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Shouldly;

namespace Malume.Api.IntegrationTests.Trips;

public sealed class TripOfflineHandoverFlowTests(MalumeApiFactory factory) : IClassFixture<MalumeApiFactory>
{
    private readonly MalumeApiFactory _factory = factory;

    [Fact]
    public async Task DriverTripManifestOfflineSyncAndSignalR_EndToEnd_ShouldSucceed()
    {
        DateOnly todaySast = SastClock.TodaySast(DateTimeOffset.UtcNow);
        DateOnly expiry = todaySast.AddYears(2);

        using HttpClient adminClient = await CreateAuthenticatedClientAsync("sysadmin@malume.test", "MalumeAdmin!2026");
        using HttpClient fleetClient = await RegisterAndAuthenticateAsync(
            $"fleet-{Guid.NewGuid():N}@malume.co.za",
            "Fleet Owner",
            MalumeRoles.FleetOwner);
        using HttpClient schoolClient = await RegisterAndAuthenticateAsync(
            $"school-{Guid.NewGuid():N}@malume.co.za",
            "School Admin",
            MalumeRoles.SchoolAdmin);
        using HttpClient driverClient = await RegisterAndAuthenticateAsync(
            $"driver-{Guid.NewGuid():N}@malume.co.za",
            "Sipho Driver",
            MalumeRoles.Driver);
        using HttpClient parentClient = await RegisterAndAuthenticateAsync(
            $"parent-{Guid.NewGuid():N}@malume.co.za",
            "Thandi Parent",
            MalumeRoles.Parent);

        // 1. Create school & fleet & vehicle & driver profile
        SchoolDto school = await PostAsync<CreateSchoolRequest, SchoolDto>(
            schoolClient,
            "/api/v1/schools",
            new CreateSchoolRequest("Orlando High School", "101 Adams St, Soweto", -26.2358, 27.9082, null),
            HttpStatusCode.Created);

        await PostAsync<CreateFleetRequest, FleetDto>(
            fleetClient,
            "/api/v1/fleets",
            new CreateFleetRequest("Soweto Quantum Fleet", "2026/001122/07"),
            HttpStatusCode.Created);

        VehicleDto vehicle = await PostAsync<CreateVehicleRequest, VehicleDto>(
            fleetClient,
            "/api/v1/vehicles",
            new CreateVehicleRequest($"GP-{Guid.NewGuid():N}"[..9], "Toyota Quantum 15-Seater", 15, expiry, expiry),
            HttpStatusCode.Created);

        DriverProfileDto driverProfile = await PostAsync<CreateDriverProfileRequest, DriverProfileDto>(
            driverClient,
            "/api/v1/drivers/me",
            new CreateDriverProfileRequest($"PRDP-{Guid.NewGuid():N}"[..16], expiry, expiry),
            HttpStatusCode.Created);

        ComplianceDocumentDto prdpDoc = await PostAsync<SubmitComplianceDocumentRequest, ComplianceDocumentDto>(
            driverClient,
            "/api/v1/compliance/documents",
            new SubmitComplianceDocumentRequest(nameof(ComplianceDocumentType.Prdp), "PRDP-99", "https://malume.test/prdp.pdf", expiry, null),
            HttpStatusCode.Created);

        ComplianceDocumentDto policeDoc = await PostAsync<SubmitComplianceDocumentRequest, ComplianceDocumentDto>(
            driverClient,
            "/api/v1/compliance/documents",
            new SubmitComplianceDocumentRequest(nameof(ComplianceDocumentType.PoliceClearance), "SAPS-99", "https://malume.test/saps.pdf", expiry, null),
            HttpStatusCode.Created);

        // 2. Parent registers learner & gets QR badge + Daily PIN
        LearnerDto learner = await PostAsync<CreateLearnerRequest, LearnerDto>(
            parentClient,
            "/api/v1/learners",
            new CreateLearnerRequest(
                school.Id,
                "Amahle Mokoena",
                "Grade 5",
                -26.2412,
                27.9015,
                "42 Vilakazi St, Orlando West",
                "+27820001111"),
            HttpStatusCode.Created);

        LearnerQrBadgeDto qrBadge = (await parentClient.GetFromJsonAsync<LearnerQrBadgeDto>(
            $"/api/v1/learners/{learner.LearnerId}/qr-badge"))!;
        qrBadge.ShouldNotBeNull();
        qrBadge.QrPayload.ShouldStartWith($"MLM1:{learner.LearnerId:D}:{school.Id:D}:");

        DailyPinDto dailyPin = (await parentClient.GetFromJsonAsync<DailyPinDto>(
            $"/api/v1/learners/{learner.LearnerId}/daily-pin?serviceDate={todaySast:yyyy-MM-dd}&direction=MorningToSchool"))!;
        dailyPin.ShouldNotBeNull();
        dailyPin.PinCode.Length.ShouldBe(4);
        dailyPin.VerifierHash.Length.ShouldBe(64);

        // 3. Create scheduled route, enroll learner, and generate daily trip
        ScheduledRouteDto route = await PostAsync<CreateScheduledRouteRequest, ScheduledRouteDto>(
            fleetClient,
            "/api/v1/routes",
            new CreateScheduledRouteRequest(
                vehicle.Id,
                driverProfile.Id,
                school.Id,
                "Orlando West Morning Corridor",
                nameof(RouteDirection.MorningToSchool),
                [new GeoPointDto(-26.245, 27.899), new GeoPointDto(-26.2358, 27.9082)],
                1200m),
            HttpStatusCode.Created);

        RouteEnrollmentDto enrollment = await PostAsync<EnrollLearnerOnRouteRequest, RouteEnrollmentDto>(
            parentClient,
            $"/api/v1/routes/{route.RouteId}/enrollments",
            new EnrollLearnerOnRouteRequest(learner.LearnerId, new DateOnly(todaySast.Year, todaySast.Month, 1)),
            HttpStatusCode.Created);
        enrollment.TotalAmountZar.ShouldBe(1200m);

        TripManifestDto createdManifest = await PostAsync<CreateDailyTripRequest, TripManifestDto>(
            fleetClient,
            "/api/v1/trips",
            new CreateDailyTripRequest(route.RouteId, todaySast),
            HttpStatusCode.Created);
        createdManifest.Stops.Count.ShouldBe(1);
        createdManifest.Stops[0].DailyPinVerifierHash.ShouldBe(dailyPin.VerifierHash);
        createdManifest.Stops[0].QrPublicKeyVerifier.ShouldBe(qrBadge.QrPublicKeyBase64);

        // 4. Compliance gate: PendingReview driver cannot start trip
        HttpResponseMessage blockedStart = await driverClient.PostAsync($"/api/v1/trips/{createdManifest.TripId}/start", null);
        blockedStart.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // SysAdmin verifies docs and approves driver
        (await adminClient.PostAsync($"/api/v1/compliance/documents/{prdpDoc.Id}/verify", null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await adminClient.PostAsync($"/api/v1/compliance/documents/{policeDoc.Id}/verify", null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await adminClient.PostAsync($"/api/v1/compliance/drivers/{driverProfile.Id}/approve", null)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Driver starts trip cleanly now
        List<DriverTripSummaryDto>? schedule = await driverClient.GetFromJsonAsync<List<DriverTripSummaryDto>>(
            $"/api/v1/trips/driver/schedule?serviceDate={todaySast:yyyy-MM-dd}");
        schedule.ShouldNotBeNull();
        schedule.Count.ShouldBe(1);

        HttpResponseMessage startedResp = await driverClient.PostAsync($"/api/v1/trips/{createdManifest.TripId}/start", null);
        startedResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        DriverTripSummaryDto? startedTrip = await startedResp.Content.ReadFromJsonAsync<DriverTripSummaryDto>();
        startedTrip!.Status.ShouldBe(nameof(TripLifecycleStatus.EnRoute));

        // 5. Offline batch handover sync (QR PickedUp + FallbackPin DroppedOff) + idempotent replay
        Guid pickupEventId = Guid.NewGuid();
        Guid dropoffEventId = Guid.NewGuid();
        var syncRequest = new BatchSyncHandoversRequest(
            [
                new HandoverEventItemRequest(
                    pickupEventId,
                    learner.LearnerId,
                    nameof(AttendanceStatus.PickedUp),
                    nameof(HandoverMethod.QrScan),
                    DateTimeOffset.UtcNow.AddMinutes(-10),
                    -26.2412,
                    27.9015,
                    WasSyncedOffline: true,
                    QrPayload: qrBadge.QrPayload),
                new HandoverEventItemRequest(
                    dropoffEventId,
                    learner.LearnerId,
                    nameof(AttendanceStatus.DroppedOff),
                    nameof(HandoverMethod.FallbackPin),
                    DateTimeOffset.UtcNow.AddMinutes(-1),
                    -26.2358,
                    27.9082,
                    WasSyncedOffline: true,
                    FallbackPin: dailyPin.PinCode)
            ]);

        BatchSyncHandoversResponse firstSync = await PostAsync<BatchSyncHandoversRequest, BatchSyncHandoversResponse>(
            driverClient,
            $"/api/v1/trips/{createdManifest.TripId}/handovers/batch-sync",
            syncRequest,
            HttpStatusCode.OK);
        firstSync.AppliedCount.ShouldBe(2);
        firstSync.DuplicateCount.ShouldBe(0);
        firstSync.RejectedCount.ShouldBe(0);

        BatchSyncHandoversResponse replaySync = await PostAsync<BatchSyncHandoversRequest, BatchSyncHandoversResponse>(
            driverClient,
            $"/api/v1/trips/{createdManifest.TripId}/handovers/batch-sync",
            syncRequest,
            HttpStatusCode.OK);
        replaySync.AppliedCount.ShouldBe(0);
        replaySync.DuplicateCount.ShouldBe(2);
        replaySync.RejectedCount.ShouldBe(0);

        // 6. SOS + gate arrival + trip completion
        HttpResponseMessage sosResp = await driverClient.PostAsJsonAsync(
            $"/api/v1/trips/{createdManifest.TripId}/sos",
            new TriggerSosRequest(-26.238, 27.905, "Roadblock emergency drill"));
        sosResp.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        (await driverClient.PostAsync($"/api/v1/trips/{createdManifest.TripId}/arrive-gate", null))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        HttpResponseMessage completeResp = await driverClient.PostAsync($"/api/v1/trips/{createdManifest.TripId}/complete", null);
        completeResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await completeResp.Content.ReadFromJsonAsync<DriverTripSummaryDto>())!.Status.ShouldBe(nameof(TripLifecycleStatus.Completed));

        // 7. SignalR live telemetry check
        string driverToken = driverClient.DefaultRequestHeaders.Authorization!.Parameter!;
        string parentToken = parentClient.DefaultRequestHeaders.Authorization!.Parameter!;

        var locationReceived = new TaskCompletionSource<DriverTelemetryPingDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        Uri hubUri = new(_factory.Server.BaseAddress, "/hubs/trips");

        await using HubConnection parentHub = new HubConnectionBuilder()
            .WithUrl(hubUri, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(parentToken);
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();

        parentHub.On<DriverTelemetryPingDto>("LocationUpdated", ping => locationReceived.TrySetResult(ping));
        await parentHub.StartAsync();
        await parentHub.InvokeAsync("JoinTripGroup", createdManifest.TripId);

        await using HubConnection driverHub = new HubConnectionBuilder()
            .WithUrl(hubUri, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(driverToken);
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();

        await driverHub.StartAsync();
        var telemetryPing = new DriverTelemetryPingDto(
            createdManifest.TripId,
            -26.237,
            27.907,
            34.5,
            120.0,
            IsSosActive: false,
            DateTimeOffset.UtcNow);
        await driverHub.InvokeAsync("StreamLocation", telemetryPing);

        DriverTelemetryPingDto received = await locationReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
        received.TripId.ShouldBe(createdManifest.TripId);
        received.Latitude.ShouldBe(-26.237);
        received.Longitude.ShouldBe(27.907);
    }

    private async Task<HttpClient> RegisterAndAuthenticateAsync(string email, string fullName, string role)
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequest(email, fullName, "+27821234567", "MalumePass!2026", role));
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        AuthResponse? auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        auth.ShouldNotBeNull();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string email, string password)
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(email, password));
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        AuthResponse? auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        auth.ShouldNotBeNull();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private static async Task<TResponse> PostAsync<TRequest, TResponse>(
        HttpClient client,
        string url,
        TRequest payload,
        HttpStatusCode expectedStatus)
        where TResponse : class
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(url, payload);
        response.StatusCode.ShouldBe(expectedStatus);
        TResponse? body = await response.Content.ReadFromJsonAsync<TResponse>();
        body.ShouldNotBeNull();
        return body;
    }
}
