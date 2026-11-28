using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Malume.Api.IntegrationTests.Infrastructure;
using Malume.Domain.Common;
using Malume.Domain.Enums;
using Malume.Shared.Contracts.Auth;
using Malume.Shared.Contracts.Compliance;
using Malume.Shared.Contracts.Corridor;
using Malume.Shared.Contracts.Drivers;
using Malume.Shared.Contracts.Endorsements;
using Malume.Shared.Contracts.Learners;
using Malume.Shared.Contracts.Routes;
using Malume.Shared.Contracts.Tenants;
using Malume.Shared.Contracts.Trips;
using Malume.Shared.Contracts.Vehicles;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Shouldly;

namespace Malume.Api.IntegrationTests.Corridor;

public sealed class CorridorAndGateBoardFlowTests(MalumeApiFactory factory) : IClassFixture<MalumeApiFactory>
{
    private readonly MalumeApiFactory _factory = factory;

    [Fact]
    public async Task SpatialCorridorBooking_MarkAbsentEtaCascade_AndGateBoard_ShouldWorkEndToEnd()
    {
        DateOnly todaySast = SastClock.TodaySast(DateTimeOffset.UtcNow);
        DateOnly expiry = todaySast.AddYears(2);

        using HttpClient adminClient = await LoginAsync("sysadmin@malume.test", "MalumeAdmin!2026");
        using HttpClient fleetClient = await RegisterAsync($"fleet4-{Guid.NewGuid():N}@malume.co.za", "Fleet 4", MalumeRoles.FleetOwner);
        using HttpClient schoolClient = await RegisterAsync($"school4-{Guid.NewGuid():N}@malume.co.za", "School 4", MalumeRoles.SchoolAdmin);
        string driverEmail = $"driver4-{Guid.NewGuid():N}@malume.co.za";
        using HttpClient driverClient = await RegisterAsync(driverEmail, "Driver 4", MalumeRoles.Driver);
        using HttpClient regularParentClient = await RegisterAsync($"parent4a-{Guid.NewGuid():N}@malume.co.za", "Parent A", MalumeRoles.Parent);
        using HttpClient corridorParentClient = await RegisterAsync($"parent4b-{Guid.NewGuid():N}@malume.co.za", "Parent B", MalumeRoles.Parent);

        // 1. Setup school, fleet, vehicle, driver, SGB endorsement
        SchoolDto school = await PostAsync<CreateSchoolRequest, SchoolDto>(
            schoolClient,
            "/api/v1/schools",
            new CreateSchoolRequest("Klipspruit Secondary", "10 Main Rd, Soweto", -26.2358, 27.9082, null, 150),
            HttpStatusCode.Created);

        await PostAsync<CreateFleetRequest, FleetDto>(
            fleetClient,
            "/api/v1/fleets",
            new CreateFleetRequest("Klipspruit Transport", null),
            HttpStatusCode.Created);

        VehicleDto vehicle = await PostAsync<CreateVehicleRequest, VehicleDto>(
            fleetClient,
            "/api/v1/vehicles",
            new CreateVehicleRequest($"GP4-{Guid.NewGuid():N}"[..9], "Toyota Quantum", 15, expiry, expiry),
            HttpStatusCode.Created);

        DriverProfileDto driverProfile = await PostAsync<CreateDriverProfileRequest, DriverProfileDto>(
            driverClient,
            "/api/v1/drivers/me",
            new CreateDriverProfileRequest($"PRDP4-{Guid.NewGuid():N}"[..16], expiry, expiry),
            HttpStatusCode.Created);

        (await fleetClient.PostAsJsonAsync("/api/v1/fleets/mine/drivers", new HireDriverRequest(driverEmail)))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await fleetClient.PutAsJsonAsync($"/api/v1/vehicles/{vehicle.Id}/driver", new AssignVehicleDriverRequest(driverProfile.Id)))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        ComplianceDocumentDto prdpDoc = await PostAsync<SubmitComplianceDocumentRequest, ComplianceDocumentDto>(
            driverClient,
            "/api/v1/compliance/documents",
            new SubmitComplianceDocumentRequest(nameof(ComplianceDocumentType.Prdp), "P4", "https://malume.test/p4.pdf", expiry, null),
            HttpStatusCode.Created);
        ComplianceDocumentDto policeDoc = await PostAsync<SubmitComplianceDocumentRequest, ComplianceDocumentDto>(
            driverClient,
            "/api/v1/compliance/documents",
            new SubmitComplianceDocumentRequest(nameof(ComplianceDocumentType.PoliceClearance), "S4", "https://malume.test/s4.pdf", expiry, null),
            HttpStatusCode.Created);

        (await adminClient.PostAsync($"/api/v1/compliance/documents/{prdpDoc.Id}/verify", null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await adminClient.PostAsync($"/api/v1/compliance/documents/{policeDoc.Id}/verify", null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await adminClient.PostAsync($"/api/v1/compliance/drivers/{driverProfile.Id}/approve", null)).StatusCode.ShouldBe(HttpStatusCode.OK);

        await PostAsync<EndorseDriverRequest, SchoolEndorsementDto>(
            schoolClient,
            "/api/v1/schools/mine/endorsements",
            new EndorseDriverRequest(driverProfile.Id, "SGB Trusted"),
            HttpStatusCode.OK);

        // 2. Register 2 regular learners along corridor + 1 ad-hoc learner near corridor + 1 far learner (> 4 km away)
        LearnerDto absentLearner = await PostAsync<CreateLearnerRequest, LearnerDto>(
            regularParentClient,
            "/api/v1/learners",
            new CreateLearnerRequest(school.Id, "Amina Absent", "Grade 4", -26.2450, 27.9082, "10 South Rd", "+27820001001"),
            HttpStatusCode.Created);

        LearnerDto downstreamLearner = await PostAsync<CreateLearnerRequest, LearnerDto>(
            regularParentClient,
            "/api/v1/learners",
            new CreateLearnerRequest(school.Id, "Zinhle Downstream", "Grade 6", -26.2700, 27.9082, "80 Deep South Rd", "+27820001002"),
            HttpStatusCode.Created);

        LearnerDto corridorLearner = await PostAsync<CreateLearnerRequest, LearnerDto>(
            corridorParentClient,
            "/api/v1/learners",
            new CreateLearnerRequest(school.Id, "Bongi Corridor", "Grade 5", -26.2460, 27.9120, "15 Corridor Close", "+27820001003"),
            HttpStatusCode.Created);

        LearnerDto farLearner = await PostAsync<CreateLearnerRequest, LearnerDto>(
            corridorParentClient,
            "/api/v1/learners",
            new CreateLearnerRequest(school.Id, "Far Learner", "Grade 5", -26.1500, 28.0500, "Sandton Far Away", "+27820001004"),
            HttpStatusCode.Created);

        ScheduledRouteDto route = await PostAsync<CreateScheduledRouteRequest, ScheduledRouteDto>(
            fleetClient,
            "/api/v1/routes",
            new CreateScheduledRouteRequest(
                vehicle.Id,
                driverProfile.Id,
                school.Id,
                "Klipspruit South Afternoon Corridor",
                nameof(RouteDirection.AfternoonFromSchool),
                [new GeoPointDto(-26.2358, 27.9082), new GeoPointDto(-26.2700, 27.9082)],
                1100m),
            HttpStatusCode.Created);

        DateOnly billingStart = new(todaySast.Year, todaySast.Month, 1);
        await PostAsync<EnrollLearnerOnRouteRequest, RouteEnrollmentDto>(
            regularParentClient,
            $"/api/v1/routes/{route.RouteId}/enrollments",
            new EnrollLearnerOnRouteRequest(absentLearner.LearnerId, billingStart),
            HttpStatusCode.Created);
        await PostAsync<EnrollLearnerOnRouteRequest, RouteEnrollmentDto>(
            regularParentClient,
            $"/api/v1/routes/{route.RouteId}/enrollments",
            new EnrollLearnerOnRouteRequest(downstreamLearner.LearnerId, billingStart),
            HttpStatusCode.Created);

        TripManifestDto trip = await PostAsync<CreateDailyTripRequest, TripManifestDto>(
            fleetClient,
            "/api/v1/trips",
            new CreateDailyTripRequest(route.RouteId, todaySast),
            HttpStatusCode.Created);

        // 3. Spatial STDistance search inside 1.5 km vs > 1.5 km
        List<CorridorAdHocOptionDto>? nearOptions = await corridorParentClient.GetFromJsonAsync<List<CorridorAdHocOptionDto>>(
            $"/api/v1/corridor/options?schoolId={school.Id}&serviceDate={todaySast:yyyy-MM-dd}&pickupLatitude=-26.2460&pickupLongitude=27.9120&maxDistanceMeters=1500");
        nearOptions.ShouldNotBeNull();
        nearOptions.ShouldContain(o => o.TripId == trip.TripId && o.HasSgbEndorsement && o.DistanceMeters <= 1500.0);

        List<CorridorAdHocOptionDto>? farOptions = await corridorParentClient.GetFromJsonAsync<List<CorridorAdHocOptionDto>>(
            $"/api/v1/corridor/options?schoolId={school.Id}&serviceDate={todaySast:yyyy-MM-dd}&pickupLatitude=-26.1500&pickupLongitude=28.0500&maxDistanceMeters=1500");
        farOptions.ShouldNotBeNull();
        farOptions.ShouldBeEmpty();

        // Reject far learner booking outside 1.5km, allow near corridor learner
        HttpResponseMessage rejectedBook = await corridorParentClient.PostAsJsonAsync(
            $"/api/v1/corridor/trips/{trip.TripId}/book",
            new BookCorridorSeatRequest(farLearner.LearnerId));
        rejectedBook.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        CorridorBookingResultDto booked = await PostAsync<BookCorridorSeatRequest, CorridorBookingResultDto>(
            corridorParentClient,
            $"/api/v1/corridor/trips/{trip.TripId}/book",
            new BookCorridorSeatRequest(corridorLearner.LearnerId),
            HttpStatusCode.Created);
        booked.RemainingCorridorSeats.ShouldBe(12); // 15 - 3 learners

        // 4. SignalR + Mark Absent Today ETA cascade
        string regularParentToken = regularParentClient.DefaultRequestHeaders.Authorization!.Parameter!;
        var etaCascadeTcs = new TaskCompletionSource<EtaCascadeUpdateDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        Uri hubUri = new(_factory.Server.BaseAddress, "/hubs/trips");

        await using HubConnection parentHub = new HubConnectionBuilder()
            .WithUrl(hubUri, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(regularParentToken);
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();
        parentHub.On<EtaCascadeUpdateDto>("EtaCascadeUpdated", dto => etaCascadeTcs.TrySetResult(dto));
        await parentHub.StartAsync();
        await parentHub.InvokeAsync("JoinParentNotificationsGroup");

        MarkLearnerAbsentResponse absentResult = await PostAsync<object, MarkLearnerAbsentResponse>(
            regularParentClient,
            $"/api/v1/trips/{trip.TripId}/learners/{absentLearner.LearnerId}/mark-absent",
            new { },
            HttpStatusCode.OK);

        absentResult.AvailableCorridorSeats.ShouldBe(13); // Released +1 seat
        absentResult.DownstreamEtaUpdates.ShouldNotBeEmpty();

        EtaCascadeUpdateDto pushedCascade = await etaCascadeTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
        pushedCascade.MinutesEarlier.ShouldBeGreaterThanOrEqualTo(2);

        // 5. Off-Peak driver mode & 5km proximity + School Gate-Arrival Board
        (await driverClient.PostAsync($"/api/v1/trips/{trip.TripId}/start", null)).StatusCode.ShouldBe(HttpStatusCode.OK);

        string driverToken = driverClient.DefaultRequestHeaders.Authorization!.Parameter!;
        await using HubConnection driverHub = new HubConnectionBuilder()
            .WithUrl(hubUri, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(driverToken);
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();
        await driverHub.StartAsync();
        await driverHub.InvokeAsync(
            "StreamLocation",
            new DriverTelemetryPingDto(trip.TripId, -26.2359, 27.9082, 12.0, 90.0, false, DateTimeOffset.UtcNow));

        SchoolGateBoardDto? gateBoard = await schoolClient.GetFromJsonAsync<SchoolGateBoardDto>(
            $"/api/v1/schools/{school.Id}/gate-board?serviceDate={todaySast:yyyy-MM-dd}");
        gateBoard.ShouldNotBeNull();
        gateBoard.Vehicles.Count.ShouldBe(1);
        gateBoard.Vehicles[0].GateZoneStatus.ShouldBe("AtPickupZone");
        gateBoard.Vehicles[0].TotalManifestCount.ShouldBe(2); // absent learner excluded

        HttpResponseMessage modeResp = await driverClient.PutAsJsonAsync(
            "/api/v1/drivers/me/operating-mode",
            new UpdateOperatingModeRequest(nameof(DriverOperatingMode.OffPeakAdHoc), -26.2400, 27.9100));
        modeResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        List<OffPeakDriverOptionDto>? offPeakDrivers = await corridorParentClient.GetFromJsonAsync<List<OffPeakDriverOptionDto>>(
            "/api/v1/corridor/off-peak-drivers?pickupLatitude=-26.2450&pickupLongitude=27.9120&maxDistanceMeters=5000");
        offPeakDrivers.ShouldNotBeNull();
        offPeakDrivers.ShouldContain(d => d.DriverProfileId == driverProfile.Id);
    }

    private async Task<HttpClient> RegisterAsync(string email, string fullName, string role)
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

    private async Task<HttpClient> LoginAsync(string email, string password)
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
