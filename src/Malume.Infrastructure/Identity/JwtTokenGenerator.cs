using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Malume.Application.Abstractions.Identity;
using Malume.Domain.Entities;
using Malume.Shared.Contracts.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Malume.Infrastructure.Identity;

internal sealed class JwtTokenGenerator(IOptions<JwtOptions> options) : IJwtTokenGenerator
{
    private readonly JwtOptions _options = options.Value;

    public GeneratedJwtToken GenerateToken(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrWhiteSpace(_options.SigningKey) || Encoding.UTF8.GetByteCount(_options.SigningKey) < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey must be configured with at least 32 bytes.");
        }

        DateTimeOffset expiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(_options.AccessTokenExpirationMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString("D", CultureInfo.InvariantCulture)),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture)),
            new(ClaimTypes.NameIdentifier, user.Id.ToString("D", CultureInfo.InvariantCulture)),
            new(ClaimTypes.Role, user.Role.ToString()),
            new(MalumeClaimTypes.FullName, user.FullName)
        };

        if (user.TenantFleetId is Guid fleetId)
        {
            claims.Add(new Claim(MalumeClaimTypes.TenantFleetId, fleetId.ToString("D", CultureInfo.InvariantCulture)));
        }

        if (user.TenantSchoolId is Guid schoolId)
        {
            claims.Add(new Claim(MalumeClaimTypes.TenantSchoolId, schoolId.ToString("D", CultureInfo.InvariantCulture)));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAtUtc.UtcDateTime,
            signingCredentials: credentials);

        string serialized = new JwtSecurityTokenHandler().WriteToken(token);
        return new GeneratedJwtToken(serialized, expiresAtUtc);
    }
}
