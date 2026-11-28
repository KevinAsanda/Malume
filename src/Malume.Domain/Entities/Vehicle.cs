using Malume.Domain.Common;
using Malume.Domain.Enums;
using Malume.Domain.Exceptions;

namespace Malume.Domain.Entities;

public sealed class Vehicle : AuditableEntity
{
    private Vehicle()
    {
    }

    public Vehicle(
        string registrationNumber,
        string makeModel,
        int totalSeatCapacity,
        DateOnly licenseDiscExpiryDate,
        DateOnly cofExpiryDate,
        Guid? fleetId = null,
        Guid? schoolId = null,
        Guid? assignedDriverId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(registrationNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(makeModel);

        if (totalSeatCapacity <= 0)
        {
            throw new DomainException("Vehicle total seat capacity must be at least 1.");
        }

        RegistrationNumber = registrationNumber.Trim().ToUpperInvariant();
        MakeModel = makeModel.Trim();
        TotalSeatCapacity = totalSeatCapacity;
        LicenseDiscExpiryDate = licenseDiscExpiryDate;
        CofExpiryDate = cofExpiryDate;
        FleetId = fleetId;
        SchoolId = schoolId;
        AssignedDriverId = assignedDriverId;
        IsActive = true;
    }

    public Guid? FleetId { get; private set; }

    public Guid? SchoolId { get; private set; }

    public Guid? AssignedDriverId { get; private set; }

    public string RegistrationNumber { get; private set; } = string.Empty;

    public string MakeModel { get; private set; } = string.Empty;

    public int TotalSeatCapacity { get; private set; }

    public DateOnly LicenseDiscExpiryDate { get; private set; }

    public DateOnly CofExpiryDate { get; private set; }

    public bool IsActive { get; private set; }

    public void AssignDriver(Guid? driverProfileId)
    {
        AssignedDriverId = driverProfileId;
        MarkUpdated();
    }

    public void UpdateDocumentExpiry(ComplianceDocumentType type, DateOnly expiry)
    {
        switch (type)
        {
            case ComplianceDocumentType.LicenseDisc:
                LicenseDiscExpiryDate = expiry;
                break;
            case ComplianceDocumentType.CertificateOfFitness:
                CofExpiryDate = expiry;
                break;
            default:
                throw new DomainException($"Document type '{type}' is not a vehicle compliance document.");
        }

        MarkUpdated();
    }
}
