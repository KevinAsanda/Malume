using System.Globalization;
using System.Net.Http.Json;
using System.Security.Claims;
using Malume.Shared.Contracts.Auth;
using Microsoft.AspNetCore.Components.Authorization;

namespace Malume.Web.Portal.Services;

public sealed class JwtAuthenticationStateProvider(
    TokenStore tokenStore,
    HttpClient httpClient,
    TimeProvider timeProvider) : AuthenticationStateProvider
{
    private static readonly AuthenticationState AnonymousState =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    private readonly TokenStore _tokenStore = tokenStore;
    private readonly HttpClient _httpClient = httpClient;
    private readonly TimeProvider _timeProvider = timeProvider;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        AuthResponse? auth = await _tokenStore.GetAsync();
        if (auth is null || string.IsNullOrWhiteSpace(auth.AccessToken))
        {
            return AnonymousState;
        }

        if (auth.ExpiresAtUtc <= _timeProvider.GetUtcNow())
        {
            await _tokenStore.ClearAsync();
            return AnonymousState;
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, auth.UserId.ToString("D", CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, auth.FullName),
            new(ClaimTypes.Email, auth.Email),
            new(ClaimTypes.Role, auth.Role)
        };

        if (auth.TenantFleetId is Guid fleetId)
        {
            claims.Add(new Claim(MalumeClaimTypes.TenantFleetId, fleetId.ToString("D", CultureInfo.InvariantCulture)));
        }

        if (auth.TenantSchoolId is Guid schoolId)
        {
            claims.Add(new Claim(MalumeClaimTypes.TenantSchoolId, schoolId.ToString("D", CultureInfo.InvariantCulture)));
        }

        var identity = new ClaimsIdentity(claims, "jwt", ClaimTypes.Name, ClaimTypes.Role);
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    public async Task<bool> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
            "api/v1/auth/login",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        AuthResponse? auth = await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken: cancellationToken);
        if (auth is null)
        {
            return false;
        }

        await _tokenStore.SaveAsync(auth, cancellationToken);
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        return true;
    }

    public async Task LogoutAsync()
    {
        await _tokenStore.ClearAsync();
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}
