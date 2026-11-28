namespace Malume.Shared.Contracts.Auth;

public sealed record RegisterRequest(
    string Email,
    string FullName,
    string PhoneNumber,
    string Password,
    string Role,
    Guid? TenantFleetId = null,
    Guid? TenantSchoolId = null);

public sealed record LoginRequest(
    string Email,
    string Password);

public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    Guid UserId,
    string Email,
    string FullName,
    string Role,
    Guid? TenantFleetId,
    Guid? TenantSchoolId);

public sealed record UserClaimsResponse(
    Guid UserId,
    string Email,
    string FullName,
    string Role,
    Guid? TenantFleetId,
    Guid? TenantSchoolId);
