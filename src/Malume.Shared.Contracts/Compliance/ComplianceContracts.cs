using Malume.Shared.Contracts.Drivers;

namespace Malume.Shared.Contracts.Compliance;

public sealed record SubmitComplianceDocumentRequest(
    string DocumentType,
    string DocumentNumber,
    string StorageUrl,
    DateOnly ExpiryDate,
    Guid? VehicleId);

public sealed record ComplianceDocumentDto(
    Guid Id,
    Guid? DriverProfileId,
    Guid? VehicleId,
    string DocumentType,
    string DocumentNumber,
    string StorageUrl,
    DateOnly ExpiryDate,
    bool IsVerified,
    DateTimeOffset? VerifiedAtUtc);

public sealed record ComplianceReviewItemDto(
    DriverProfileDto Driver,
    IReadOnlyList<ComplianceDocumentDto> Documents);

public sealed record ComplianceReviewQueueDto(
    IReadOnlyList<ComplianceReviewItemDto> Drivers,
    IReadOnlyList<ComplianceDocumentDto> UnverifiedVehicleDocuments);

public sealed record ComplianceScanResultDto(
    DateOnly EvaluatedDateSast,
    int DriversEvaluated,
    int MarkedExpiringSoon,
    int Suspended,
    int SubscriptionsPaused);
