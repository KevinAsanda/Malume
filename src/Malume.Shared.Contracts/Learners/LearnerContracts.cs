namespace Malume.Shared.Contracts.Learners;

public sealed record CreateLearnerRequest(
    Guid SchoolId,
    string FullName,
    string Grade,
    double HomeLatitude,
    double HomeLongitude,
    string HomeAddressText,
    string EmergencyContactPhone);

public sealed record LearnerDto(
    Guid LearnerId,
    Guid ParentUserId,
    Guid SchoolId,
    string FullName,
    string Grade,
    double HomeLatitude,
    double HomeLongitude,
    string HomeAddressText,
    string EmergencyContactPhone);

public sealed record LearnerQrBadgeDto(
    Guid LearnerId,
    Guid SchoolId,
    string LearnerFullName,
    string QrPayload,
    string QrPublicKeyBase64);

public sealed record DailyPinDto(
    Guid LearnerId,
    DateOnly ServiceDate,
    string Direction,
    int DirectionCode,
    string PinCode,
    string VerifierHash);
