namespace Malume.Shared.Contracts.Trips;

public sealed record CreateDailyTripRequest(
    Guid RouteId,
    DateOnly ServiceDate);

public sealed record DriverTripSummaryDto(
    Guid TripId,
    Guid RouteId,
    string RouteName,
    string Direction,
    int DirectionCode,
    Guid SchoolId,
    string SchoolName,
    Guid VehicleId,
    string VehicleRegistration,
    DateOnly ServiceDate,
    string Status,
    int AvailableCorridorSeats,
    int TotalStops,
    int CompletedStops,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? ArrivedAtSchoolGateUtc,
    DateTimeOffset? CompletedAtUtc);

public sealed record TripManifestStopDto(
    Guid ManifestEntryId,
    Guid LearnerId,
    string LearnerFullName,
    string Grade,
    Guid SchoolId,
    string HomeAddressText,
    string EmergencyContactPhone,
    int SequenceOrder,
    double StopLatitude,
    double StopLongitude,
    string BookingType,
    string AttendanceStatus,
    string HandoverMethod,
    DateTimeOffset? HandoverTimestampUtc,
    DateTimeOffset? EstimatedArrivalUtc,
    string QrPublicKeyVerifier,
    string DailyPinVerifierHash,
    bool WasSyncedOffline);

public sealed record TripManifestDto(
    Guid TripId,
    Guid RouteId,
    string RouteName,
    string Direction,
    int DirectionCode,
    DateOnly ServiceDate,
    string Status,
    Guid SchoolId,
    string SchoolName,
    double SchoolGateLatitude,
    double SchoolGateLongitude,
    int SchoolGateGeofenceRadiusMeters,
    string VehicleRegistration,
    string DriverComplianceStatus,
    IReadOnlyList<TripManifestStopDto> Stops);

public sealed record HandoverEventItemRequest(
    Guid EventId,
    Guid LearnerId,
    string Action,
    string Method,
    DateTimeOffset RecordedAtUtc,
    double Latitude,
    double Longitude,
    bool WasSyncedOffline,
    string? QrPayload = null,
    string? FallbackPin = null);

public sealed record BatchSyncHandoversRequest(
    IReadOnlyList<HandoverEventItemRequest> Events);

public sealed record HandoverSyncItemResultDto(
    Guid EventId,
    Guid LearnerId,
    string Outcome,
    string? ErrorMessage);

public sealed record BatchSyncHandoversResponse(
    Guid TripId,
    int AppliedCount,
    int DuplicateCount,
    int RejectedCount,
    IReadOnlyList<HandoverSyncItemResultDto> Results);

public sealed record DriverTelemetryPingDto(
    Guid TripId,
    double Latitude,
    double Longitude,
    double SpeedKph,
    double BearingDegrees,
    bool IsSosActive,
    DateTimeOffset RecordedAtUtc);

public sealed record TriggerSosRequest(
    double Latitude,
    double Longitude,
    string? Notes = null);

public sealed record SosAlertDto(
    Guid TripId,
    Guid DriverProfileId,
    string DriverName,
    string VehicleRegistration,
    double Latitude,
    double Longitude,
    int OnboardLearnerCount,
    DateTimeOffset TriggeredAtUtc,
    string? Notes);
