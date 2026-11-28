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

public sealed record EtaCascadeUpdateDto(
    Guid LearnerId,
    Guid ParentUserId,
    string LearnerFullName,
    int NewSequenceOrder,
    DateTimeOffset? PreviousEtaUtc,
    DateTimeOffset NewEtaUtc,
    int MinutesEarlier);

public sealed record MarkLearnerAbsentResponse(
    Guid TripId,
    Guid LearnerId,
    int AvailableCorridorSeats,
    IReadOnlyList<EtaCascadeUpdateDto> DownstreamEtaUpdates);

public sealed record ParentActiveTripDto(
    Guid TripId,
    Guid RouteId,
    string RouteName,
    string Direction,
    int DirectionCode,
    DateOnly ServiceDate,
    string TripStatus,
    Guid SchoolId,
    string SchoolName,
    string DriverFullName,
    string VehicleRegistration,
    Guid LearnerId,
    string LearnerFullName,
    string AttendanceStatus,
    int SequenceOrder,
    DateTimeOffset? EstimatedArrivalUtc,
    double? DriverLatitude,
    double? DriverLongitude,
    DateTimeOffset? DriverLocationUpdatedAtUtc);

public sealed record GateLearnerSummaryDto(
    Guid LearnerId,
    string LearnerFullName,
    string Grade,
    string AttendanceStatus);

public sealed record GateArrivalVehicleDto(
    Guid TripId,
    Guid RouteId,
    string RouteName,
    string VehicleRegistration,
    string DriverFullName,
    string TripStatus,
    string GateZoneStatus,
    double? DistanceToGateMeters,
    int? EtaMinutes,
    int BoardedCount,
    int TotalManifestCount,
    DateTimeOffset? DepartedAtUtc,
    IReadOnlyList<GateLearnerSummaryDto> Learners);

public sealed record SchoolGateBoardDto(
    Guid SchoolId,
    string SchoolName,
    DateOnly ServiceDate,
    double GateLatitude,
    double GateLongitude,
    int GateGeofenceRadiusMeters,
    IReadOnlyList<GateArrivalVehicleDto> Vehicles);
