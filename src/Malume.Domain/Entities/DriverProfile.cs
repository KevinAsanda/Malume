using Malume.Domain.Common;
using Malume.Domain.Enums;
using Malume.Domain.Exceptions;
using Malume.Domain.StateMachines;
using Malume.Domain.ValueObjects;

namespace Malume.Domain.Entities;

public sealed class DriverProfile : AuditableEntity
{
    private DriverProfile()
    {
    }

    public DriverProfile(
        Guid userId,
        string prdpNumber,
        DateOnly? prdpExpiryDate = null,
        DateOnly? policeClearanceExpiryDate = null,
        Guid? fleetId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prdpNumber);

        UserId = userId;
        FleetId = fleetId;
        PrdpNumber = prdpNumber.Trim();
        PrdpExpiryDate = prdpExpiryDate;
        PoliceClearanceExpiryDate = policeClearanceExpiryDate;
        ComplianceStatus = DriverComplianceStatus.PendingReview;
        OperatingMode = DriverOperatingMode.FixedRoute;
    }

    public Guid UserId { get; private set; }

    public Guid? FleetId { get; private set; }

    public DriverComplianceStatus ComplianceStatus { get; private set; }

    public DriverOperatingMode OperatingMode { get; private set; }

    public string PrdpNumber { get; private set; } = string.Empty;

    public DateOnly? PrdpExpiryDate { get; private set; }

    public DateOnly? PoliceClearanceExpiryDate { get; private set; }

    public GeoCoordinate? CurrentLocation { get; private set; }

    public DateTimeOffset? LastLocationUpdatedAtUtc { get; private set; }

    public void ApproveCompliance(DateOnly prdpExpiryDate, DateOnly policeClearanceExpiryDate)
    {
        PrdpExpiryDate = prdpExpiryDate;
        PoliceClearanceExpiryDate = policeClearanceExpiryDate;
        ComplianceStatus = DriverComplianceStateMachine.Transition(ComplianceStatus, DriverComplianceStatus.ActiveCompliant);
        MarkUpdated();
    }

    public void MarkExpiringSoon()
    {
        ComplianceStatus = DriverComplianceStateMachine.Transition(ComplianceStatus, DriverComplianceStatus.ActiveExpiringSoon);
        MarkUpdated();
    }

    public void SuspendForExpiredDocument()
    {
        ComplianceStatus = DriverComplianceStateMachine.Transition(ComplianceStatus, DriverComplianceStatus.SuspendedExpiredDoc);
        OperatingMode = DriverOperatingMode.FixedRoute;
        MarkUpdated();
    }

    public void SuspendForSafetyIncident()
    {
        ComplianceStatus = DriverComplianceStateMachine.Transition(ComplianceStatus, DriverComplianceStatus.SuspendedSafetyIncident);
        OperatingMode = DriverOperatingMode.FixedRoute;
        MarkUpdated();
    }

    public void SetOperatingMode(DriverOperatingMode mode)
    {
        if (mode == DriverOperatingMode.OffPeakAdHoc && !DriverComplianceStateMachine.CanStartTrip(ComplianceStatus))
        {
            throw new DomainException("Driver must be NLTA compliant before enabling Off-Peak Ad-Hoc mode.");
        }

        OperatingMode = mode;
        MarkUpdated();
    }

    public void UpdateLocation(GeoCoordinate location)
    {
        ArgumentNullException.ThrowIfNull(location);
        CurrentLocation = location;
        LastLocationUpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
