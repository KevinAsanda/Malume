using System.Security.Claims;
using System.Text;
using Malume.Infrastructure.Identity;
using Malume.Shared.Contracts.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Malume.Api.Security;

public static class MalumePolicies
{
    public const string RequireParent = "RequireParent";
    public const string RequireDriver = "RequireDriver";
    public const string RequireFleetOwner = "RequireFleetOwner";
    public const string RequireSchoolAdmin = "RequireSchoolAdmin";
    public const string RequireSysAdmin = "RequireSysAdmin";
    public const string RequirePortalOperator = "RequirePortalOperator";

    public static void AddMalumePolicies(AuthorizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.AddPolicy(RequireParent, p => p.RequireRole(MalumeRoles.Parent));
        options.AddPolicy(RequireDriver, p => p.RequireRole(MalumeRoles.Driver));
        options.AddPolicy(RequireFleetOwner, p => p.RequireRole(MalumeRoles.FleetOwner));
        options.AddPolicy(RequireSchoolAdmin, p => p.RequireRole(MalumeRoles.SchoolAdmin));
        options.AddPolicy(RequireSysAdmin, p => p.RequireRole(MalumeRoles.SysAdmin));
        options.AddPolicy(
            RequirePortalOperator,
            p => p.RequireRole(MalumeRoles.FleetOwner, MalumeRoles.SchoolAdmin, MalumeRoles.SysAdmin));
    }
}

internal sealed class ConfigureJwtBearerOptions(IOptions<JwtOptions> jwtOptions)
    : IConfigureNamedOptions<JwtBearerOptions>
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    public void Configure(string? name, JwtBearerOptions options)
    {
        if (!string.Equals(name, JwtBearerDefaults.AuthenticationScheme, StringComparison.Ordinal))
        {
            return;
        }

        Configure(options);
    }

    public void Configure(JwtBearerOptions options)
    {
        if (string.IsNullOrWhiteSpace(_jwtOptions.SigningKey)
            || Encoding.UTF8.GetByteCount(_jwtOptions.SigningKey) < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey must be configured with at least 32 bytes.");
        }

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = _jwtOptions.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey)),
            ClockSkew = TimeSpan.FromMinutes(1),
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = ClaimTypes.NameIdentifier
        };
    }
}
