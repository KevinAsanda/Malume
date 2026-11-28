using Malume.Domain.Common;

namespace Malume.Domain.Entities;

public sealed class SchoolEndorsement : AuditableEntity
{
    private SchoolEndorsement()
    {
    }

    public SchoolEndorsement(Guid schoolId, Guid driverProfileId, Guid endorsedByUserId, string? notes = null)
    {
        SchoolId = schoolId;
        DriverProfileId = driverProfileId;
        EndorsedByUserId = endorsedByUserId;
        Notes = notes?.Trim();
        IsActive = true;
        EndorsedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid SchoolId { get; private set; }

    public Guid DriverProfileId { get; private set; }

    public Guid EndorsedByUserId { get; private set; }

    public string? Notes { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset EndorsedAtUtc { get; private set; }

    public void Revoke()
    {
        IsActive = false;
        MarkUpdated();
    }

    public void Reactivate(Guid endorsedByUserId, string? notes)
    {
        IsActive = true;
        EndorsedByUserId = endorsedByUserId;
        Notes = notes?.Trim();
        EndorsedAtUtc = DateTimeOffset.UtcNow;
        MarkUpdated();
    }
}
