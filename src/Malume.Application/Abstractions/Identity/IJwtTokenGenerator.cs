using Malume.Domain.Entities;

namespace Malume.Application.Abstractions.Identity;

public sealed record GeneratedJwtToken(string AccessToken, DateTimeOffset ExpiresAtUtc);

public interface IJwtTokenGenerator
{
    GeneratedJwtToken GenerateToken(User user);
}
