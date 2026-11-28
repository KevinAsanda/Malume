using Malume.Domain.Common;
using Malume.Domain.Enums;
using Malume.Domain.Exceptions;
using Malume.Domain.ValueObjects;

namespace Malume.Domain.Entities;

public sealed class ScheduledRoute : AuditableEntity
{
    private ScheduledRoute()
    {
    }

    public ScheduledRoute(
        Guid vehicleId,
        Guid driverProfileId,
        Guid schoolId,
        string routeName,
        RouteDirection direction,
        GeoCorridor corridorPath,
        ZarMoney monthlyPrice)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routeName);
        ArgumentNullException.ThrowIfNull(corridorPath);
        ArgumentNullException.ThrowIfNull(monthlyPrice);

        VehicleId = vehicleId;
        DriverProfileId = driverProfileId;
        SchoolId = schoolId;
        RouteName = routeName.Trim();
        Direction = direction;
        CorridorPath = corridorPath;
        MonthlyPrice = monthlyPrice;
        ActiveSubscribedSeats = 0;
        IsActive = true;
    }

    public Guid VehicleId { get; private set; }

    public Guid DriverProfileId { get; private set; }

    public Guid SchoolId { get; private set; }

    public string RouteName { get; private set; } = string.Empty;

    public RouteDirection Direction { get; private set; }

    public GeoCorridor CorridorPath { get; private set; } = new([new(0.0, 0.0), new(0.001, 0.001)]);

    public ZarMoney MonthlyPrice { get; private set; } = ZarMoney.Zero;

    public int ActiveSubscribedSeats { get; private set; }

    public bool IsActive { get; private set; }

    public void ReserveSeat(int totalVehicleSeatCapacity)
    {
        if (ActiveSubscribedSeats >= totalVehicleSeatCapacity)
        {
            throw new DomainException("All seats on this scheduled route vehicle are already subscribed.");
        }

        ActiveSubscribedSeats++;
        MarkUpdated();
    }

    public void ReleaseSeat()
    {
        if (ActiveSubscribedSeats > 0)
        {
            ActiveSubscribedSeats--;
            MarkUpdated();
        }
    }
}
