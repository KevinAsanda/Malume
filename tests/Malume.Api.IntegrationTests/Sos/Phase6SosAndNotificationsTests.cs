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
using Malume.Shared.Contracts.Notifications;
using Malume.Shared.Contracts.Routes;
using Malume.Shared.Contracts.Tenants;
using Malume.Shared.Contracts.Trips;
using Malume.Shared.Contracts.Vehicles;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Shouldly;

namespace Malume.Api.IntegrationTests.Sos;

public sealed class Phase6SosAndNotificationsTests(MalumeApiFactory factory) : IClassFixture<MalumeApiFactory>
{
    private readonly MalumeApiFactory _factory = factory;

    [Fact]
    public async Task ParentSos_SchoolBroadcast_AcknowledgeResolve_DeviceTokens_AndMapCoordinates_ShouldWorkEndToEnd()
    {
        DateOnly todaySast = SastClock.TodaySast(DateTimeOffset.UtcNow);
        DateOnly expiry = todaySast.AddYears(2);

        using HttpClient adminClient = await LoginAsync("sysadmin@malume.test", "MalumeAdmin!2026");
        using HttpClient fleetClient = await RegisterAsync($"fleet6-{Guid.NewGuid():N}@malume.co.za", "Fleet 6", MalumeRoles.FleetOwner);
        using HttpClient schoolClient = await RegisterAsync($"school6-{Guid.NewGuid():N}@malume.co.za", "School 6", MalumeRoles.SchoolAdmin);
        string driverEmail = $"driver6-{Guid.NewGuid():N}@malume.co.za";
        using HttpClient driverClient = await RegisterAsync(driverEmail, "Driver 6", MalumeRoles.Driver);
        using HttpClient parentClient = await RegisterAsync($"parent6-{Guid.NewGuid():N}@malume.co.za", "Parent 6", MalumeRoles.Parent);
        using HttpClient unrelatedParentClient = await RegisterAsync($"other6-{Guid.NewGuid():N}@malume.co.za", "Other Parent 6", MalumeRoles.Parent);

        // 1. Setup school, fleet, vehicle, approved driver, learner, route & daily trip
        SchoolDto school = await PostAsync<CreateSchoolRequest, SchoolDto>(
            schoolClient,
            "/api/v1/schools",
            new CreateSchoolRequest("Orlando High", "42 Vilakazi St, Soweto", -26.2380, 27.9090, null, 150),
            HttpStatusCode.Created);

        // Re-login school admin so TenantSchoolId claim is refreshed cleanly after creating the school
        string schoolToken = schoolClient.DefaultRequestHeaders.Authorization!.Parameter!;

        await PostAsync<CreateFleetRequest, FleetDto>(
            fleetClient,
            "/api/v1/fleets",
            new CreateFleetRequest("Orlando Fleet", null),
            HttpStatusCode.Created);

        VehicleDto vehicle = await PostAsync<CreateVehicleRequest, VehicleDto>(
            fleetClient,
            "/api/v1/vehicles",
            new CreateVehicleRequest($"GP6-{Guid.NewGuid():N}"[..9], "Toyota Quantum", 15, expiry, expiry),
            HttpStatusCode.Created);

        DriverProfileDto driverProfile = await PostAsync<CreateDriverProfileRequest, DriverProfileDto>(
            driverClient,
            "/api/v1/drivers/me",
            new CreateDriverProfileRequest($"PRDP6-{Guid.NewGuid():N}"[..16], expiry, expiry),
            HttpStatusCode.Created);

        (await fleetClient.PostAsJsonAsync("/api/v1/fleets/mine/drivers", new HireDriverRequest(driverEmail))).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await fleetClient.PutAsJsonAsync($"/api/v1/vehicles/{vehicle.Id}/driver", new AssignVehicleDriverRequest(driverProfile.Id))).StatusCode.ShouldBe(HttpStatusCode.OK);

        ComplianceDocumentDto prdpDoc = await PostAsync<SubmitComplianceDocumentRequest, ComplianceDocumentDto>(
            driverClient,
            "/api/v1/compliance/documents",
            new SubmitComplianceDocumentRequest(nameof(ComplianceDocumentType.Prdp), "P6", "https://malume.test/p6.pdf", expiry, null),
            HttpStatusCode.Created);
        ComplianceDocumentDto policeDoc = await PostAsync<SubmitComplianceDocumentRequest, ComplianceDocumentDto>(
            driverClient,
            "/api/v1/compliance/documents",
            new SubmitComplianceDocumentRequest(nameof(ComplianceDocumentType.PoliceClearance), "S6", "https://malume.test/s6.pdf", expiry, null),
            HttpStatusCode.Created);

        (await adminClient.PostAsync($"/api/v1/compliance/documents/{prdpDoc.Id}/verify", null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await adminClient.PostAsync($"/api/v1/compliance/documents/{policeDoc.Id}/verify", null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await adminClient.PostAsync($"/api/v1/compliance/drivers/{driverProfile.Id}/approve", null)).StatusCode.ShouldBe(HttpStatusCode.OK);

        LearnerDto learner = await PostAsync<CreateLearnerRequest, LearnerDto>(
            parentClient,
            "/api/v1/learners",
            new CreateLearnerRequest(school.Id, "Thandiwe Moyo", "Grade 5", -26.2490, 27.9120, "18 Meadowlands", "+27820006001"),
            HttpStatusCode.Created);

        ScheduledRouteDto route = await PostAsync<CreateScheduledRouteRequest, ScheduledRouteDto>(
            fleetClient,
            "/api/v1/routes",
            new CreateScheduledRouteRequest(
                vehicle.Id,
                driverProfile.Id,
                school.Id,
                "Orlando Morning Express",
                nameof(RouteDirection.MorningToSchool),
                [new GeoPointDto(-26.2490, 27.9120), new GeoPointDto(-26.2380, 27.9090)],
                1000m),
            HttpStatusCode.Created);

        DateOnly billingStart = new(todaySast.Year, todaySast.Month, 1);
        await PostAsync<EnrollLearnerOnRouteRequest, RouteEnrollmentDto>(
            parentClient,
            $"/api/v1/routes/{route.RouteId}/enrollments",
            new EnrollLearnerOnRouteRequest(learner.LearnerId, billingStart),
            HttpStatusCode.Created);

        TripManifestDto trip = await PostAsync<CreateDailyTripRequest, TripManifestDto>(
            fleetClient,
            "/api/v1/trips",
            new CreateDailyTripRequest(route.RouteId, todaySast),
            HttpStatusCode.Created);

        (await driverClient.PostAsync($"/api/v1/trips/{trip.TripId}/start", null)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // 2. Register & unregister FCM device token
        DeviceTokenRegistrationDto tokenReg = await PostAsync<RegisterDeviceTokenRequest, DeviceTokenRegistrationDto>(
            parentClient,
            "/api/v1/notifications/device-token",
            new RegisterDeviceTokenRequest("fcm-device-token-abc123", "Android", "Parent"),
            HttpStatusCode.OK);
        tokenReg.Platform.ShouldBe("Android");
        tokenReg.AppVariant.ShouldBe("Parent");

        HttpResponseMessage deleteTokenResp = await parentClient.DeleteAsync("/api/v1/notifications/device-token?token=fcm-device-token-abc123");
        deleteTokenResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // 3. Stream driver telemetry & verify map coordinates on both GateBoard & ParentActiveTrips
        Uri hubUri = new(_factory.Server.BaseAddress, "/hubs/trips");
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
            new DriverTelemetryPingDto(trip.TripId, -26.2410, 27.9100, 35.0, 45.0, false, DateTimeOffset.UtcNow));

        SchoolGateBoardDto? board = await schoolClient.GetFromJsonAsync<SchoolGateBoardDto>(
            $"/api/v1/schools/{school.Id}/gate-board?serviceDate={todaySast:yyyy-MM-dd}");
        board.ShouldNotBeNull();
        GateArrivalVehicleDto? boardVehicle = board.Vehicles.FirstOrDefault(v => v.TripId == trip.TripId);
        boardVehicle.ShouldNotBeNull();
        boardVehicle.DriverLatitude.ShouldNotBeNull();
        boardVehicle.DriverLatitude!.Value.ShouldBe(-26.2410, 0.001);
        boardVehicle.DriverLongitude!.Value.ShouldBe(27.9100, 0.001);

        List<ParentActiveTripDto>? parentTrips = await parentClient.GetFromJsonAsync<List<ParentActiveTripDto>>(
            $"/api/v1/trips/parent/active?serviceDate={todaySast:yyyy-MM-dd}");
        parentTrips.ShouldNotBeNull();
        ParentActiveTripDto? activeParentTrip = parentTrips.FirstOrDefault(t => t.TripId == trip.TripId);
        activeParentTrip.ShouldNotBeNull();
        activeParentTrip.SchoolGateLatitude.ShouldNotBeNull();
        activeParentTrip.LearnerHomeLatitude.ShouldNotBeNull();

        // 4. Connect SchoolAdmin SignalR hub to confirm SOS broadcast reaches school group
        var sosReceivedTcs = new TaskCompletionSource<SosAlertDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using HubConnection schoolHub = new HubConnectionBuilder()
            .WithUrl(hubUri, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(schoolToken);
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();
        schoolHub.On<SosAlertDto>("SosIncidentCreated", dto => sosReceivedTcs.TrySetResult(dto));
        await schoolHub.StartAsync();
        await schoolHub.InvokeAsync("JoinSchoolGateGroup", school.Id);

        // 5. Unrelated parent cannot trigger SOS (403); enrolled parent triggers SOS (202)
        HttpResponseMessage forbiddenSos = await unrelatedParentClient.PostAsJsonAsync(
            $"/api/v1/trips/{trip.TripId}/sos",
            new TriggerSosRequest(-26.2410, 27.9100, "Unauthorized test"));
        forbiddenSos.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        SosAlertDto sosAlert = await PostAsync<TriggerSosRequest, SosAlertDto>(
            parentClient,
            $"/api/v1/trips/{trip.TripId}/sos",
            new TriggerSosRequest(-26.2410, 27.9100, "Parent panic test"),
            HttpStatusCode.Accepted);

        sosAlert.IncidentId.ShouldNotBe(Guid.Empty);
        sosAlert.SchoolId.ShouldBe(school.Id);
        sosAlert.TriggeredByRole.ShouldBe("Parent");
        sosAlert.Status.ShouldBe("Active");

        SosAlertDto broadcastedAlert = await sosReceivedTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
        broadcastedAlert.IncidentId.ShouldBe(sosAlert.IncidentId);

        // 6. SchoolAdmin lists active SOS incidents, acknowledges, then resolves
        List<SosAlertDto>? activeIncidents = await schoolClient.GetFromJsonAsync<List<SosAlertDto>>("/api/v1/sos?activeOnly=true");
        activeIncidents.ShouldNotBeNull();
        activeIncidents.ShouldContain(i => i.IncidentId == sosAlert.IncidentId && i.Status == "Active");

        SosAlertDto acked = await PostAsync<object, SosAlertDto>(
            schoolClient,
            $"/api/v1/sos/{sosAlert.IncidentId}/acknowledge",
            new { },
            HttpStatusCode.OK);
        acked.Status.ShouldBe("Acknowledged");
        acked.AcknowledgedAtUtc.ShouldNotBeNull();

        SosAlertDto resolved = await PostAsync<ResolveSosIncidentRequest, SosAlertDto>(
            schoolClient,
            $"/api/v1/sos/{sosAlert.IncidentId}/resolve",
            new ResolveSosIncidentRequest("Security team confirmed learners safe."),
            HttpStatusCode.OK);
        resolved.Status.ShouldBe("Resolved");
        resolved.ResolutionNotes.ShouldBe("Security team confirmed learners safe.");

        List<SosAlertDto>? activeAfterResolve = await schoolClient.GetFromJsonAsync<List<SosAlertDto>>("/api/v1/sos?activeOnly=true");
        activeAfterResolve.ShouldNotBeNull();
        activeAfterResolve.ShouldNotContain(i => i.IncidentId == sosAlert.IncidentId);
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
