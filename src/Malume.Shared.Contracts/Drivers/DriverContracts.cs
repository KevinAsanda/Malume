namespace Malume.Shared.Contracts.Drivers;

public sealed record CreateDriverProfileRequest(
    string PrdpNumber,
    DateOnly? PrdpExpiryDate,
    DateOnly? PoliceClearanceExpiryDate);

public sealed record DriverProfileDto(
    Guid Id,
    Guid UserId,
    string FullName,
    string Email,
    Guid? FleetId,
    string ComplianceStatus,
    string OperatingMode,
    string PrdpNumber,
    DateOnly? PrdpExpiryDate,
    DateOnly? PoliceClearanceExpiryDate);
