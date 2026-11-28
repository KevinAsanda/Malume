using Malume.Domain.Common;
using Malume.Domain.Enums;
using Malume.Domain.Exceptions;
using Malume.Domain.StateMachines;

namespace Malume.Domain.Entities;

public sealed class DailyTripInstance : AuditableEntity
{
    private DailyTripInstance()
    {
    }

    public DailyTripInstance(
        Guid routeId,
        Guid driverProfileId,
        Guid vehicleId,
        DateOnly serviceDate,
        int availableCorridorSeats)
    {
        if (availableCorridorSeats < 0)
        {
            throw new DomainException("Available corridor seats cannot be negative.");
        }

        RouteId = routeId;
        DriverProfileId = driverProfileId;
        VehicleId = vehicleId;
        ServiceDate = serviceDate;
        Status = TripLifecycleStatus.Scheduled;
        AvailableCorridorSeats = availableCorridorSeats;
    }

    public Guid RouteId { get; private set; }

    public Guid DriverProfileId { get; private set; }

    public Guid VehicleId { get; private set; }

    public DateOnly ServiceDate { get; private set; }

    public TripLifecycleStatus Status { get; private set; }

    public int AvailableCorridorSeats { get; private set; }

    public DateTimeOffset? StartedAtUtc { get; private set; }

    public DateTimeOffset? ArrivedAtSchoolGateUtc { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public void StartTrip(DriverComplianceStatus driverComplianceStatus)
    {
        if (!DriverComplianceStateMachine.CanStartTrip(driverComplianceStatus))
        {
            throw new DomainException("Cannot start trip because driver NLTA compliance status is suspended or pending.");
        }

        Status = TripLifecycleStateMachine.Transition(Status, TripLifecycleStatus.EnRoute);
        StartedAtUtc = DateTimeOffset.UtcNow;
        MarkUpdated();
    }

    public void ArriveAtSchoolGate()
    {
        Status = TripLifecycleStateMachine.Transition(Status, TripLifecycleStatus.AtSchoolGate);
        ArrivedAtSchoolGateUtc = DateTimeOffset.UtcNow;
        MarkUpdated();
    }

    public void CompleteTrip()
    {
        Status = TripLifecycleStateMachine.Transition(Status, TripLifecycleStatus.Completed);
        CompletedAtUtc = DateTimeOffset.UtcNow;
        MarkUpdated();
    }

    public void DecrementAvailableCorridorSeat()
    {
        if (AvailableCorridorSeats <= 0)
        {
            throw new DomainException("No empty corridor seats remain on this trip.");
        }

        AvailableCorridorSeats--;
        MarkUpdated();
    }

    public void IncrementAvailableCorridorSeat()
    {
        AvailableCorridorSeats++;
        MarkUpdated();
    }
}
