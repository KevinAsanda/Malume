namespace Malume.Shared.Contracts.Corridor;

public sealed record CorridorAdHocOptionDto(
    Guid TripId,
    Guid RouteId,
    string RouteName,
    string Direction,
    DateOnly ServiceDate,
    Guid DriverProfileId,
    string DriverFullName,
    string VehicleMakeModel,
    string VehicleRegistration,
    int AvailableCorridorSeats,
    double DistanceMeters,
    bool HasSgbEndorsement);

public sealed record BookCorridorSeatRequest(
    Guid LearnerId,
    double? PickupLatitude = null,
    double? PickupLongitude = null);

public sealed record CorridorBookingResultDto(
    Guid TripId,
    Guid ManifestEntryId,
    Guid LearnerId,
    int SequenceOrder,
    int RemainingCorridorSeats,
    DateTimeOffset? EstimatedArrivalUtc);

public sealed record OffPeakDriverOptionDto(
    Guid DriverProfileId,
    string DriverFullName,
    Guid VehicleId,
    string VehicleMakeModel,
    string VehicleRegistration,
    double DistanceMeters);

public sealed record UpdateOperatingModeRequest(
    string OperatingMode,
    double? CurrentLatitude = null,
    double? CurrentLongitude = null);
