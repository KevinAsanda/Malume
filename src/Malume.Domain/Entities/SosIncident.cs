using Malume.Domain.Common;
using Malume.Domain.Enums;
using Malume.Domain.Exceptions;
using Malume.Domain.ValueObjects;

namespace Malume.Domain.Entities;

public sealed class SosIncident : AuditableEntity
{
    private SosIncident()
    {
    }

    public SosIncident(
        Guid tripId,
        Guid schoolId,
        Guid driverProfileId,
        Guid triggeredByUserId,
        UserRole triggeredByRole,
        string driverFullName,
        string vehicleRegistration,
        GeoCoordinate location,
        int onboardLearnerCount,
        DateTimeOffset triggeredAtUtc,
        string? notes = null)
    {
        if (tripId == Guid.Empty)
        {
            throw new DomainException("SOS incident trip ID cannot be empty.");
        }

        if (schoolId == Guid.Empty)
        {
            throw new DomainException("SOS incident school ID cannot be empty.");
        }

        if (driverProfileId == Guid.Empty)
        {
            throw new DomainException("SOS incident driver profile ID cannot be empty.");
        }

        if (triggeredByUserId == Guid.Empty)
        {
            throw new DomainException("SOS incident triggering user ID cannot be empty.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(driverFullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(vehicleRegistration);
        ArgumentNullException.ThrowIfNull(location);

        if (onboardLearnerCount < 0)
        {
            throw new DomainException("Onboard learner count cannot be negative.");
        }

        TripId = tripId;
        SchoolId = schoolId;
        DriverProfileId = driverProfileId;
        TriggeredByUserId = triggeredByUserId;
        TriggeredByRole = triggeredByRole;
        DriverFullName = driverFullName.Trim();
        VehicleRegistration = vehicleRegistration.Trim();
        Location = location;
        OnboardLearnerCount = onboardLearnerCount;
        TriggeredAtUtc = triggeredAtUtc;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        Status = SosIncidentStatus.Active;
    }

    public Guid TripId { get; private set; }

    public Guid SchoolId { get; private set; }

    public Guid DriverProfileId { get; private set; }

    public Guid TriggeredByUserId { get; private set; }

    public UserRole TriggeredByRole { get; private set; }

    public string DriverFullName { get; private set; } = string.Empty;

    public string VehicleRegistration { get; private set; } = string.Empty;

    public GeoCoordinate Location { get; private set; } = new(0.0, 0.0);

    public int OnboardLearnerCount { get; private set; }

    public string? Notes { get; private set; }

    public SosIncidentStatus Status { get; private set; }

    public DateTimeOffset TriggeredAtUtc { get; private set; }

    public DateTimeOffset? AcknowledgedAtUtc { get; private set; }

    public DateTimeOffset? ResolvedAtUtc { get; private set; }

    public Guid? ResolvedByUserId { get; private set; }

    public string? ResolutionNotes { get; private set; }

    public void Acknowledge(DateTimeOffset atUtc)
    {
        if (Status == SosIncidentStatus.Resolved)
        {
            throw new DomainException("Cannot acknowledge an SOS incident that has already been resolved.");
        }

        Status = SosIncidentStatus.Acknowledged;
        AcknowledgedAtUtc ??= atUtc;
        MarkUpdated();
    }

    public void Resolve(Guid resolvedByUserId, string? resolutionNotes, DateTimeOffset atUtc)
    {
        if (resolvedByUserId == Guid.Empty)
        {
            throw new DomainException("Resolving user ID cannot be empty.");
        }

        if (Status == SosIncidentStatus.Resolved)
        {
            throw new DomainException("SOS incident has already been resolved.");
        }

        Status = SosIncidentStatus.Resolved;
        ResolvedByUserId = resolvedByUserId;
        ResolvedAtUtc = atUtc;
        ResolutionNotes = string.IsNullOrWhiteSpace(resolutionNotes) ? null : resolutionNotes.Trim();
        AcknowledgedAtUtc ??= atUtc;
        MarkUpdated();
    }
}
