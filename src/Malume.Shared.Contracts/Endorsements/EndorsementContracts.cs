namespace Malume.Shared.Contracts.Endorsements;

public sealed record EndorseDriverRequest(
    Guid DriverProfileId,
    string? Notes);

public sealed record SchoolEndorsementDto(
    Guid Id,
    Guid SchoolId,
    Guid DriverProfileId,
    string DriverFullName,
    string? Notes,
    bool IsActive,
    DateTimeOffset EndorsedAtUtc);
