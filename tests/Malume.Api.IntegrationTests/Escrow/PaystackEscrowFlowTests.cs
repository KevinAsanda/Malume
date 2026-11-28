using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Malume.Api.IntegrationTests.Infrastructure;
using Malume.Domain.Common;
using Malume.Domain.Enums;
using Malume.Shared.Contracts.Auth;
using Malume.Shared.Contracts.Compliance;
using Malume.Shared.Contracts.Drivers;
using Malume.Shared.Contracts.Escrow;
using Malume.Shared.Contracts.Learners;
using Malume.Shared.Contracts.Routes;
using Malume.Shared.Contracts.Tenants;
using Malume.Shared.Contracts.Trips;
using Malume.Shared.Contracts.Vehicles;
using Shouldly;

namespace Malume.Api.IntegrationTests.Escrow;

public sealed class PaystackEscrowFlowTests(MalumeApiFactory factory) : IClassFixture<MalumeApiFactory>
{
    private readonly MalumeApiFactory _factory = factory;

    [Fact]
    public async Task StagedEscrowLifecycle_ChargeWebhook_Tranches_SuspensionFreeze_AndRefund_ShouldBalanceLedger()
    {
        DateOnly todaySast = SastClock.TodaySast(DateTimeOffset.UtcNow);
        DateOnly monthStart = new(todaySast.Year, todaySast.Month, 1);
        DateOnly expiry = todaySast.AddYears(2);

        using HttpClient adminClient = await LoginAsync("sysadmin@malume.test", "MalumeAdmin!2026");
        using HttpClient fleetClient = await RegisterAsync($"fleet5-{Guid.NewGuid():N}@malume.co.za", "Fleet 5 Owner", MalumeRoles.FleetOwner);
        using HttpClient schoolClient = await RegisterAsync($"school5-{Guid.NewGuid():N}@malume.co.za", "School 5 Admin", MalumeRoles.SchoolAdmin);
        string driverEmail = $"driver5-{Guid.NewGuid():N}@malume.co.za";
        using HttpClient driverClient = await RegisterAsync(driverEmail, "Driver 5", MalumeRoles.Driver);
        using HttpClient parentClient = await RegisterAsync($"parent5-{Guid.NewGuid():N}@malume.co.za", "Parent 5", MalumeRoles.Parent);

        // 1. Setup school, fleet + subaccount, vehicle, compliant driver, learner & route (R1,200/month)
        SchoolDto school = await PostAsync<CreateSchoolRequest, SchoolDto>(
            schoolClient,
            "/api/v1/schools",
            new CreateSchoolRequest("Dobsonville Primary", "55 Main Rd, Soweto", -26.2240, 27.8680, null),
            HttpStatusCode.Created);

        await PostAsync<CreateFleetRequest, FleetDto>(
            fleetClient,
            "/api/v1/fleets",
            new CreateFleetRequest("Dobsonville Coaches", null),
            HttpStatusCode.Created);

        FleetDto subaccountFleet = await PostAsync<ConfigureFleetSubaccountRequest, FleetDto>(
            fleetClient,
            "/api/v1/payments/fleets/mine/subaccount",
            new ConfigureFleetSubaccountRequest("Dobsonville Coaches", "632005", "4012345678", 5.0m),
            HttpStatusCode.OK);
        subaccountFleet.PaystackSubaccountCode.ShouldNotBeNullOrWhiteSpace();

        VehicleDto vehicle = await PostAsync<CreateVehicleRequest, VehicleDto>(
            fleetClient,
            "/api/v1/vehicles",
            new CreateVehicleRequest($"GP5-{Guid.NewGuid():N}"[..9], "Toyota Quantum", 15, expiry, expiry),
            HttpStatusCode.Created);

        DriverProfileDto driverProfile = await PostAsync<CreateDriverProfileRequest, DriverProfileDto>(
            driverClient,
            "/api/v1/drivers/me",
            new CreateDriverProfileRequest($"PRDP5-{Guid.NewGuid():N}"[..16], expiry, expiry),
            HttpStatusCode.Created);

        (await fleetClient.PostAsJsonAsync("/api/v1/fleets/mine/drivers", new HireDriverRequest(driverEmail))).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await fleetClient.PutAsJsonAsync($"/api/v1/vehicles/{vehicle.Id}/driver", new AssignVehicleDriverRequest(driverProfile.Id))).StatusCode.ShouldBe(HttpStatusCode.OK);

        ComplianceDocumentDto prdpDoc = await PostAsync<SubmitComplianceDocumentRequest, ComplianceDocumentDto>(
            driverClient,
            "/api/v1/compliance/documents",
            new SubmitComplianceDocumentRequest(nameof(ComplianceDocumentType.Prdp), "P5", "https://malume.test/p5.pdf", expiry, null),
            HttpStatusCode.Created);
        ComplianceDocumentDto policeDoc = await PostAsync<SubmitComplianceDocumentRequest, ComplianceDocumentDto>(
            driverClient,
            "/api/v1/compliance/documents",
            new SubmitComplianceDocumentRequest(nameof(ComplianceDocumentType.PoliceClearance), "S5", "https://malume.test/s5.pdf", expiry, null),
            HttpStatusCode.Created);

        (await adminClient.PostAsync($"/api/v1/compliance/documents/{prdpDoc.Id}/verify", null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await adminClient.PostAsync($"/api/v1/compliance/documents/{policeDoc.Id}/verify", null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await adminClient.PostAsync($"/api/v1/compliance/drivers/{driverProfile.Id}/approve", null)).StatusCode.ShouldBe(HttpStatusCode.OK);

        LearnerDto learner = await PostAsync<CreateLearnerRequest, LearnerDto>(
            parentClient,
            "/api/v1/learners",
            new CreateLearnerRequest(school.Id, "Naledi Khumalo", "Grade 3", -26.2280, 27.8650, "20 Dobsonville", "+27820005001"),
            HttpStatusCode.Created);

        ScheduledRouteDto route = await PostAsync<CreateScheduledRouteRequest, ScheduledRouteDto>(
            fleetClient,
            "/api/v1/routes",
            new CreateScheduledRouteRequest(
                vehicle.Id,
                driverProfile.Id,
                school.Id,
                "Dobsonville Morning Shuttle",
                nameof(RouteDirection.MorningToSchool),
                [new GeoPointDto(-26.2280, 27.8650), new GeoPointDto(-26.2240, 27.8680)],
                1200m),
            HttpStatusCode.Created);

        // 2. Parent initializes Paystack payment & tests HMAC-SHA512 webhook + verify endpoint
        InitializeSubscriptionPaymentResponse initPayment = await PostAsync<InitializeSubscriptionPaymentRequest, InitializeSubscriptionPaymentResponse>(
            parentClient,
            "/api/v1/payments/subscriptions/initialize",
            new InitializeSubscriptionPaymentRequest(route.RouteId, learner.LearnerId, monthStart),
            HttpStatusCode.OK);
        initPayment.TotalAmountZar.ShouldBe(1200m);

        string webhookBody = $"{{\"event\":\"charge.success\",\"data\":{{\"reference\":\"{initPayment.PaystackReference}\"}}}}";
        string validSignature = ComputePaystackHmac("malume-test-webhook-secret", webhookBody);

        using HttpClient anonClient = _factory.CreateClient();
        using var badWebhookReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/paystack/webhook")
        {
            Content = new StringContent(webhookBody, Encoding.UTF8, "application/json")
        };
        badWebhookReq.Headers.Add("x-paystack-signature", "deadbeef");
        (await anonClient.SendAsync(badWebhookReq)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        using var goodWebhookReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/paystack/webhook")
        {
            Content = new StringContent(webhookBody, Encoding.UTF8, "application/json")
        };
        goodWebhookReq.Headers.Add("x-paystack-signature", validSignature);
        (await anonClient.SendAsync(goodWebhookReq)).StatusCode.ShouldBe(HttpStatusCode.OK);

        VerifySubscriptionPaymentResponse verifyResp = await PostAsync<object, VerifySubscriptionPaymentResponse>(
            parentClient,
            $"/api/v1/payments/subscriptions/{initPayment.PaystackReference}/verify",
            new { },
            HttpStatusCode.OK);
        verifyResp.PaymentVerified.ShouldBeTrue();

        // 3. Run Staged Payouts on 1st of month -> releases Tranche 1 (40% = R480) to Fleet Owner
        EscrowPayoutRunResultDto tranche1Run = await PostAsync<object, EscrowPayoutRunResultDto>(
            adminClient,
            $"/api/v1/admin/jobs/escrow-payouts?asOfDate={monthStart:yyyy-MM-dd}",
            new { },
            HttpStatusCode.OK);
        tranche1Run.MilestonesReleased.ShouldBeGreaterThanOrEqualTo(1);

        WalletLedgerSummaryDto? fleetLedgerAfterT1 = await fleetClient.GetFromJsonAsync<WalletLedgerSummaryDto>("/api/v1/escrow/ledger");
        fleetLedgerAfterT1.ShouldNotBeNull();
        fleetLedgerAfterT1.TotalCreditsZar.ShouldBe(480m);

        // 4. On 15th of month before trips are completed, Tranche 2 (30% = R360) is skipped (< 85% ratio)
        DateOnly midMonthDate = monthStart.AddDays(14);
        EscrowPayoutRunResultDto skippedT2Run = await PostAsync<object, EscrowPayoutRunResultDto>(
            adminClient,
            $"/api/v1/admin/jobs/escrow-payouts?asOfDate={midMonthDate:yyyy-MM-dd}",
            new { },
            HttpStatusCode.OK);
        skippedT2Run.TranchesSkippedBelowRatio.ShouldBeGreaterThanOrEqualTo(1);

        // Create & complete 1 trip in window -> 100% completion ratio -> Tranche 2 releases R360 (total R840)
        TripManifestDto midTrip = await PostAsync<CreateDailyTripRequest, TripManifestDto>(
            fleetClient,
            "/api/v1/trips",
            new CreateDailyTripRequest(route.RouteId, monthStart),
            HttpStatusCode.Created);
        (await driverClient.PostAsync($"/api/v1/trips/{midTrip.TripId}/start", null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await driverClient.PostAsync($"/api/v1/trips/{midTrip.TripId}/arrive-gate", null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await driverClient.PostAsync($"/api/v1/trips/{midTrip.TripId}/complete", null)).StatusCode.ShouldBe(HttpStatusCode.OK);

        EscrowPayoutRunResultDto releasedT2Run = await PostAsync<object, EscrowPayoutRunResultDto>(
            adminClient,
            $"/api/v1/admin/jobs/escrow-payouts?asOfDate={midMonthDate:yyyy-MM-dd}",
            new { },
            HttpStatusCode.OK);
        releasedT2Run.MilestonesReleased.ShouldBeGreaterThanOrEqualTo(1);

        WalletLedgerSummaryDto? fleetLedgerAfterT2 = await fleetClient.GetFromJsonAsync<WalletLedgerSummaryDto>("/api/v1/escrow/ledger");
        fleetLedgerAfterT2.ShouldNotBeNull();
        fleetLedgerAfterT2.TotalCreditsZar.ShouldBe(840m); // 480 + 360

        // 5. Suspend driver mid-month -> pauses subscription & freezes Tranche 3 (R360) -> Admin issues 1-click Pro-Rata Refund
        (await adminClient.PostAsync($"/api/v1/compliance/drivers/{driverProfile.Id}/suspend", null)).StatusCode.ShouldBe(HttpStatusCode.OK);

        List<SubscriptionEscrowDto>? subs = await adminClient.GetFromJsonAsync<List<SubscriptionEscrowDto>>("/api/v1/escrow/subscriptions");
        subs.ShouldNotBeNull();
        SubscriptionEscrowDto? pausedSub = subs.FirstOrDefault(s => s.SubscriptionId == initPayment.SubscriptionId);
        pausedSub.ShouldNotBeNull();
        pausedSub.EscrowStatus.ShouldBe(nameof(EscrowStatus.PausedComplianceOrServiceFailure));

        ProRataRefundResponse refund = await PostAsync<ProRataRefundRequest, ProRataRefundResponse>(
            adminClient,
            $"/api/v1/escrow/subscriptions/{initPayment.SubscriptionId}/pro-rata-refund",
            new ProRataRefundRequest("Driver suspended mid-month"),
            HttpStatusCode.OK);
        refund.RefundedAmountZar.ShouldBe(360m);
        refund.RefundedMilestoneCount.ShouldBe(1);

        WalletLedgerSummaryDto? parentLedger = await parentClient.GetFromJsonAsync<WalletLedgerSummaryDto>("/api/v1/escrow/ledger");
        parentLedger.ShouldNotBeNull();
        parentLedger.TotalCreditsZar.ShouldBe(360m);
    }

    private static string ComputePaystackHmac(string secret, string payload)
    {
        byte[] hash = HMACSHA512.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
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
