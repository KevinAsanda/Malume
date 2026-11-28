namespace Malume.Infrastructure.Identity;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string SigningKey { get; set; } = string.Empty;

    public string Issuer { get; set; } = "Malume.Api";

    public string Audience { get; set; } = "Malume.Clients";

    public int AccessTokenExpirationMinutes { get; set; } = 60;
}
