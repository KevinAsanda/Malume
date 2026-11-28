using Malume.Domain.Common;
using Malume.Domain.Enums;

namespace Malume.Domain.Entities;

public sealed class ComplianceDocument : AuditableEntity
{
    private ComplianceDocument()
    {
    }

    public ComplianceDocument(
        ComplianceDocumentType documentType,
        string documentNumber,
        string storageUrl,
        DateOnly expiryDate,
        Guid? driverProfileId = null,
        Guid? vehicleId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(storageUrl);

        DocumentType = documentType;
        DocumentNumber = documentNumber.Trim();
        StorageUrl = storageUrl.Trim();
        ExpiryDate = expiryDate;
        DriverProfileId = driverProfileId;
        VehicleId = vehicleId;
    }

    public Guid? DriverProfileId { get; private set; }

    public Guid? VehicleId { get; private set; }

    public ComplianceDocumentType DocumentType { get; private set; }

    public string DocumentNumber { get; private set; } = string.Empty;

    public string StorageUrl { get; private set; } = string.Empty;

    public DateOnly ExpiryDate { get; private set; }

    public bool IsVerified { get; private set; }

    public Guid? VerifiedByUserId { get; private set; }

    public DateTimeOffset? VerifiedAtUtc { get; private set; }

    public void Verify(Guid reviewerUserId)
    {
        IsVerified = true;
        VerifiedByUserId = reviewerUserId;
        VerifiedAtUtc = DateTimeOffset.UtcNow;
        MarkUpdated();
    }
}
