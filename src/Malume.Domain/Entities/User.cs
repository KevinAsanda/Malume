using Malume.Domain.Common;
using Malume.Domain.Enums;

namespace Malume.Domain.Entities;

public sealed class User : AuditableEntity
{
    private User()
    {
    }

    public User(
        string email,
        string fullName,
        string phoneNumber,
        string passwordHash,
        UserRole role,
        Guid? tenantFleetId = null,
        Guid? tenantSchoolId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        Email = email.Trim();
        NormalizedEmail = Email.ToUpperInvariant();
        FullName = fullName.Trim();
        PhoneNumber = phoneNumber.Trim();
        PasswordHash = passwordHash;
        Role = role;
        TenantFleetId = tenantFleetId;
        TenantSchoolId = tenantSchoolId;
        IsActive = true;
    }

    public string Email { get; private set; } = string.Empty;

    public string NormalizedEmail { get; private set; } = string.Empty;

    public string FullName { get; private set; } = string.Empty;

    public string PhoneNumber { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public UserRole Role { get; private set; }

    public Guid? TenantFleetId { get; private set; }

    public Guid? TenantSchoolId { get; private set; }

    public bool IsActive { get; private set; }

    public void AssignFleetTenant(Guid fleetId)
    {
        TenantFleetId = fleetId;
        MarkUpdated();
    }

    public void AssignSchoolTenant(Guid schoolId)
    {
        TenantSchoolId = schoolId;
        MarkUpdated();
    }

    public void Deactivate()
    {
        IsActive = false;
        MarkUpdated();
    }
}
