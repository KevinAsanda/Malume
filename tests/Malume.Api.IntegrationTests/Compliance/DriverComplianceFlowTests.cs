using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Malume.Api.IntegrationTests.Infrastructure;
using Malume.Domain.Common;
using Malume.Domain.Enums;
using Malume.Shared.Contracts.Auth;
using Malume.Shared.Contracts.Compliance;
using Malume.Shared.Contracts.Drivers;
using Shouldly;

namespace Malume.Api.IntegrationTests.Compliance;

public sealed class DriverComplianceFlowTests(MalumeApiFactory factory) : IClassFixture<MalumeApiFactory>
{
    private readonly MalumeApiFactory _factory = factory;

    [Fact]
    public async Task DriverCompliance_SubmitVerifyAndApprove_ShouldTransitionToActiveCompliant()
    {
        using HttpClient driverClient = _factory.CreateClient();
        string driverEmail = $"driver-{Guid.NewGuid():N}@malume.co.za";

        HttpResponseMessage regResponse = await driverClient.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequest(driverEmail, "Sipho Ndlovu", "+27821110001", "MalumePass!2026", MalumeRoles.Driver));
        regResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        AuthResponse? driverAuth = await regResponse.Content.ReadFromJsonAsync<AuthResponse>();
        driverAuth.ShouldNotBeNull();
        driverClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", driverAuth.AccessToken);

        HttpResponseMessage profileResponse = await driverClient.PostAsJsonAsync(
            "/api/v1/drivers/me",
            new CreateDriverProfileRequest($"PRDP-{Guid.NewGuid():N}"[..16], null, null));
        profileResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        DriverProfileDto? createdProfile = await profileResponse.Content.ReadFromJsonAsync<DriverProfileDto>();
        createdProfile.ShouldNotBeNull();

        DateOnly expiry = SastClock.TodaySast(DateTimeOffset.UtcNow).AddYears(2);

        HttpResponseMessage prdpResponse = await driverClient.PostAsJsonAsync(
            "/api/v1/compliance/documents",
            new SubmitComplianceDocumentRequest(
                nameof(ComplianceDocumentType.Prdp),
                "PRDP-DOC-001",
                "https://storage.malume.test/prdp.pdf",
                expiry,
                null));
        prdpResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        ComplianceDocumentDto? prdpDoc = await prdpResponse.Content.ReadFromJsonAsync<ComplianceDocumentDto>();
        prdpDoc.ShouldNotBeNull();

        HttpResponseMessage policeResponse = await driverClient.PostAsJsonAsync(
            "/api/v1/compliance/documents",
            new SubmitComplianceDocumentRequest(
                nameof(ComplianceDocumentType.PoliceClearance),
                "SAPS-DOC-001",
                "https://storage.malume.test/police.pdf",
                expiry,
                null));
        policeResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        ComplianceDocumentDto? policeDoc = await policeResponse.Content.ReadFromJsonAsync<ComplianceDocumentDto>();
        policeDoc.ShouldNotBeNull();

        using HttpClient adminClient = _factory.CreateClient();
        HttpResponseMessage loginResponse = await adminClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest("sysadmin@malume.test", "MalumeAdmin!2026"));
        loginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        AuthResponse? adminAuth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        adminAuth.ShouldNotBeNull();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth.AccessToken);

        ComplianceReviewQueueDto? queue = await adminClient.GetFromJsonAsync<ComplianceReviewQueueDto>("/api/v1/compliance/review-queue");
        queue.ShouldNotBeNull();
        queue.Drivers.ShouldContain(item => item.Driver.Id == createdProfile.Id && item.Documents.Count == 2);

        (await adminClient.PostAsync($"/api/v1/compliance/documents/{prdpDoc.Id}/verify", null))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await adminClient.PostAsync($"/api/v1/compliance/documents/{policeDoc.Id}/verify", null))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        HttpResponseMessage approveResponse = await adminClient.PostAsync(
            $"/api/v1/compliance/drivers/{createdProfile.Id}/approve",
            null);
        approveResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        DriverProfileDto? me = await driverClient.GetFromJsonAsync<DriverProfileDto>("/api/v1/drivers/me");
        me.ShouldNotBeNull();
        me.ComplianceStatus.ShouldBe(nameof(DriverComplianceStatus.ActiveCompliant));
    }

    [Fact]
    public async Task ReviewQueue_WhenNonSysAdmin_ShouldReturnForbidden()
    {
        using HttpClient client = _factory.CreateClient();
        HttpResponseMessage regResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequest(
                $"driver-forbidden-{Guid.NewGuid():N}@malume.co.za",
                "Non Admin Driver",
                "+27821110002",
                "MalumePass!2026",
                MalumeRoles.Driver));
        AuthResponse? auth = await regResponse.Content.ReadFromJsonAsync<AuthResponse>();
        auth.ShouldNotBeNull();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        HttpResponseMessage response = await client.GetAsync("/api/v1/compliance/review-queue");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
