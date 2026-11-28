namespace Malume.Shared.Contracts.Routes;

public sealed record GeoPointDto(
    double Latitude,
    double Longitude);

public sealed record CreateScheduledRouteRequest(
    Guid VehicleId,
    Guid DriverProfileId,
    Guid SchoolId,
    string RouteName,
    string Direction,
    IReadOnlyList<GeoPointDto> CorridorWaypoints,
    decimal MonthlyPriceZar);

public sealed record ScheduledRouteDto(
    Guid RouteId,
    Guid VehicleId,
    Guid DriverProfileId,
    Guid SchoolId,
    string RouteName,
    string Direction,
    decimal MonthlyPriceZar,
    int ActiveSubscribedSeats,
    bool IsActive);

public sealed record EnrollLearnerOnRouteRequest(
    Guid LearnerId,
    DateOnly? BillingMonthStart);

public sealed record RouteEnrollmentDto(
    Guid SubscriptionId,
    Guid RouteId,
    Guid LearnerId,
    string BillingYearMonth,
    decimal TotalAmountZar,
    string EscrowStatus);
