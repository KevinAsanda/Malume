using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Malume.Api.IntegrationTests.Infrastructure;
using Malume.Shared.Contracts.Auth;
using Shouldly;

namespace Malume.Api.IntegrationTests.Auth;

public sealed class AuthAndRbacIntegrationTests(MalumeApiFactory factory) : IClassFixture<MalumeApiFactory>
{
    private readonly MalumeApiFactory _factory = factory;

    [Fact]
    public async Task RegisterLoginAndMe_ShouldIssueValidJwtWithRoleClaims()
    {
        using HttpClient client = _factory.CreateClient();
        string email = $"parent-{Guid.NewGuid():N}@malume.co.za";

        var registerRequest = new RegisterRequest(
            email,
            "Thandiwe Dlamini",
            "+27823334444",
            "MalumePass!2026",
            MalumeRoles.Parent);

        HttpResponseMessage registerResponse = await client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);
        registerResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        AuthResponse? registered = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        registered.ShouldNotBeNull();
        registered.AccessToken.ShouldNotBeNullOrWhiteSpace();
        registered.Role.ShouldBe(MalumeRoles.Parent);

        HttpResponseMessage loginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(email, "MalumePass!2026"));
        loginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        AuthResponse? loggedIn = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        loggedIn.ShouldNotBeNull();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loggedIn.AccessToken);
        HttpResponseMessage meResponse = await client.GetAsync("/api/v1/auth/me");
        meResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        UserClaimsResponse? me = await meResponse.Content.ReadFromJsonAsync<UserClaimsResponse>();
        me.ShouldNotBeNull();
        me.Email.ShouldBe(email);
        me.Role.ShouldBe(MalumeRoles.Parent);
    }

    [Fact]
    public async Task ProtectedEndpoints_ShouldEnforceJwtAndRolePolicies()
    {
        using HttpClient anonymousClient = _factory.CreateClient();
        HttpResponseMessage unauthenticated = await anonymousClient.GetAsync("/api/v1/auth/me");
        unauthenticated.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        using HttpClient parentClient = _factory.CreateClient();
        HttpResponseMessage parentReg = await parentClient.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequest(
                $"parent2-{Guid.NewGuid():N}@malume.co.za",
                "Parent Two",
                "+27825556666",
                "MalumePass!2026",
                MalumeRoles.Parent));
        AuthResponse? parentAuth = await parentReg.Content.ReadFromJsonAsync<AuthResponse>();
        parentAuth.ShouldNotBeNull();
        parentClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", parentAuth.AccessToken);

        (await parentClient.GetAsync("/api/v1/auth/rbac/fleet")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await parentClient.GetAsync("/api/v1/auth/rbac/sysadmin")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using HttpClient fleetClient = _factory.CreateClient();
        HttpResponseMessage fleetReg = await fleetClient.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequest(
                $"fleet-{Guid.NewGuid():N}@malume.co.za",
                "Fleet Owner One",
                "+27827778888",
                "MalumePass!2026",
                MalumeRoles.FleetOwner));
        AuthResponse? fleetAuth = await fleetReg.Content.ReadFromJsonAsync<AuthResponse>();
        fleetAuth.ShouldNotBeNull();
        fleetClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", fleetAuth.AccessToken);

        (await fleetClient.GetAsync("/api/v1/auth/rbac/fleet")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await fleetClient.GetAsync("/api/v1/auth/rbac/sysadmin")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Register_AsSysAdmin_ShouldReturnBadRequest()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequest(
                $"sysadmin-{Guid.NewGuid():N}@malume.co.za",
                "Blocked Admin",
                "+27829990000",
                "MalumePass!2026",
                MalumeRoles.SysAdmin));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
