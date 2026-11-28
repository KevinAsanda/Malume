using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Malume.Api.IntegrationTests.Infrastructure;
using Malume.Domain.Common;
using Malume.Domain.Entities;
using Malume.Domain.Enums;
using Malume.Shared.Contracts.Auth;
using Malume.Shared.Contracts.Drivers;
using Malume.Shared.Contracts.Endorsements;
using Malume.Shared.Contracts.Tenants;
using Malume.Shared.Contracts.Vehicles;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Malume.Api.IntegrationTests.Tenancy;

public sealed class FleetVehicleTenancyTests(MalumeApiFactory factory) : IClassFixture<MalumeApiFactory>
{
    private readonly MalumeApiFactory _factory = factory;

    [Fact]
    public async Task FleetOwnerIsolation_AndSchoolEndorsementRules_ShouldBeEnforced()
    {
        using HttpClient ownerAClient = _factory.CreateClient();
        AuthResponse ownerAAuth = await RegisterAndAuthorizeAsync(
            ownerAClient,
            $"ownerA-{Guid.NewGuid():N}@malume.test",
            "Owner A",
            MalumeRoles.FleetOwner);

        (await ownerAClient.PostAsJsonAsync(
            "/api/v1/fleets",
            new CreateFleetRequest("Owner A Fleet", "REG-A-001")))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        HttpResponseMessage vehicleResponse = await ownerAClient.PostAsJsonAsync(
            "/api/v1/vehicles",
            new CreateVehicleRequest(
                $"FLT{Guid.NewGuid().ToString("N")[..5].ToUpperInvariant()}",
                "Toyota Quantum",
                15,
                new DateOnly(2028, 1, 1),
                new DateOnly(2028, 1, 1)));
        vehicleResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        VehicleDto? vehicle = await vehicleResponse.Content.ReadFromJsonAsync<VehicleDto>();
        vehicle.ShouldNotBeNull();

        using HttpClient driverClient = _factory.CreateClient();
        string driverEmail = $"hired-driver-{Guid.NewGuid():N}@malume.test";
        await RegisterAndAuthorizeAsync(driverClient, driverEmail, "Hired Driver", MalumeRoles.Driver);
        HttpResponseMessage driverProfileResp = await driverClient.PostAsJsonAsync(
            "/api/v1/drivers/me",
            new CreateDriverProfileRequest($"PRDP-{Guid.NewGuid():N}"[..16], null, null));
        driverProfileResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        DriverProfileDto? driverProfile = await driverProfileResp.Content.ReadFromJsonAsync<DriverProfileDto>();
        driverProfile.ShouldNotBeNull();

        using HttpClient ownerBClient = _factory.CreateClient();
        await RegisterAndAuthorizeAsync(
            ownerBClient,
            $"ownerB-{Guid.NewGuid():N}@malume.test",
            "Owner B",
            MalumeRoles.FleetOwner);
        (await ownerBClient.PostAsJsonAsync(
            "/api/v1/fleets",
            new CreateFleetRequest("Owner B Fleet", "REG-B-001")))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        HttpResponseMessage crossTenantAssign = await ownerBClient.PutAsJsonAsync(
            $"/api/v1/vehicles/{vehicle.Id}/driver",
            new AssignVehicleDriverRequest(driverProfile.Id));
        crossTenantAssign.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        HttpResponseMessage hireResp = await ownerAClient.PostAsJsonAsync(
            "/api/v1/fleets/mine/drivers",
            new HireDriverRequest(driverEmail));
        hireResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        HttpResponseMessage assignResp = await ownerAClient.PutAsJsonAsync(
            $"/api/v1/vehicles/{vehicle.Id}/driver",
            new AssignVehicleDriverRequest(driverProfile.Id));
        assignResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        VehicleDto? assignedVehicle = await assignResp.Content.ReadFromJsonAsync<VehicleDto>();
        assignedVehicle.ShouldNotBeNull();
        assignedVehicle.AssignedDriverId.ShouldBe(driverProfile.Id);

        using HttpClient schoolClient = _factory.CreateClient();
        await RegisterAndAuthorizeAsync(
            schoolClient,
            $"school-{Guid.NewGuid():N}@malume.test",
            "SGB Admin",
            MalumeRoles.SchoolAdmin);
        (await schoolClient.PostAsJsonAsync(
            "/api/v1/schools",
            new CreateSchoolRequest("SGB Test School", "10 School Ave", -26.15, 28.05, "EMIS-99")))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        HttpResponseMessage unapprovedEndorse = await schoolClient.PostAsJsonAsync(
            "/api/v1/schools/mine/endorsements",
            new EndorseDriverRequest(driverProfile.Id, "Should fail while pending"));
        unapprovedEndorse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        Guid approvedDriverId = await SeedApprovedDriverAsync();
        HttpResponseMessage validEndorse = await schoolClient.PostAsJsonAsync(
            "/api/v1/schools/mine/endorsements",
            new EndorseDriverRequest(approvedDriverId, "SGB Endorsed"));
        validEndorse.StatusCode.ShouldBe(HttpStatusCode.OK);
        SchoolEndorsementDto? endorsement = await validEndorse.Content.ReadFromJsonAsync<SchoolEndorsementDto>();
        endorsement.ShouldNotBeNull();
        endorsement.IsActive.ShouldBeTrue();
        ownerAAuth.ShouldNotBeNull();
    }

    private async Task<Guid> SeedApprovedDriverAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Malume.Infrastructure.Persistence.MalumeDbContext>();
        DateOnly today = SastClock.TodaySast(DateTimeOffset.UtcNow);

        var user = new User($"endorsed-{Guid.NewGuid():N}@malume.test", "Compliant Driver", "+27820009999", "hash", UserRole.Driver);
        var driver = new DriverProfile(user.Id, $"PRDP-END-{Guid.NewGuid():N}"[..16]);
        driver.ApproveCompliance(today.AddYears(1), today.AddYears(1));

        db.Users.Add(user);
        db.DriverProfiles.Add(driver);
        await db.SaveChangesAsync();
        return driver.Id;
    }

    private static async Task<AuthResponse> RegisterAndAuthorizeAsync(
        HttpClient client,
        string email,
        string fullName,
        string role)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequest(email, fullName, "+27821234567", "MalumePass!2026", role));
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        AuthResponse? auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        auth.ShouldNotBeNull();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return auth;
    }
}
