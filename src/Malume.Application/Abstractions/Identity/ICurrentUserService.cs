namespace Malume.Application.Abstractions.Identity;

public interface ICurrentUserService
{
    Guid? UserId { get; }

    string? Email { get; }

    string? Role { get; }

    Guid? TenantFleetId { get; }

    Guid? TenantSchoolId { get; }
}
