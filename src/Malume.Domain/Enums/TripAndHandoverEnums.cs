namespace Malume.Domain.Enums;

public enum RouteDirection
{
    MorningToSchool = 1,
    AfternoonFromSchool = 2
}

public enum TripLifecycleStatus
{
    Scheduled = 1,
    EnRoute = 2,
    AtSchoolGate = 3,
    Completed = 4,
    Cancelled = 5
}

public enum BookingType
{
    MonthlyContract = 1,
    AdHocCorridor = 2,
    AdHocOffPeak = 3
}

public enum AttendanceStatus
{
    Scheduled = 1,
    MarkedAbsent = 2,
    PickedUp = 3,
    DroppedOff = 4
}

public enum HandoverMethod
{
    None = 0,
    QrScan = 1,
    FallbackPin = 2
}

public enum SosIncidentStatus
{
    Active = 0,
    Acknowledged = 1,
    Resolved = 2
}
