using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Malume.Application.Abstractions.Identity;
using Malume.Shared.Contracts.Auth;

namespace Malume.Api.Services;

internal sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public Guid? UserId => ParseGuidClaim(ClaimTypes.NameIdentifier) ?? ParseGuidClaim(JwtRegisteredClaimNames.Sub);

    public string? Email =>
        Principal?.FindFirstValue(ClaimTypes.Email) ?? Principal?.FindFirstValue(JwtRegisteredClaimNames.Email);

    public string? Role => Principal?.FindFirstValue(ClaimTypes.Role);

    public Guid? TenantFleetId => ParseGuidClaim(MalumeClaimTypes.TenantFleetId);

    public Guid? TenantSchoolId => ParseGuidClaim(MalumeClaimTypes.TenantSchoolId);

    private Guid? ParseGuidClaim(string claimType)
    {
        string? raw = Principal?.FindFirstValue(claimType);
        return Guid.TryParse(raw, out Guid parsed) ? parsed : null;
    }
}
