using Malume.Domain.Entities;
using Malume.Shared.Contracts.Compliance;
using Malume.Shared.Contracts.Drivers;
using Malume.Shared.Contracts.Endorsements;
using Malume.Shared.Contracts.Tenants;
using Malume.Shared.Contracts.Vehicles;

namespace Malume.Application.Common.Mappings;

public static class DtoMappings
{
    public static FleetDto ToDto(this Fleet fleet)
    {
        ArgumentNullException.ThrowIfNull(fleet);

        return new FleetDto(
            fleet.Id,
            fleet.Name,
            fleet.CompanyRegistrationNumber,
            fleet.PaystackSubaccountCode,
            fleet.OwnerUserId,
            fleet.IsActive);
    }

    public static SchoolDto ToDto(this School school)
    {
        ArgumentNullException.ThrowIfNull(school);

        return new SchoolDto(
            school.Id,
            school.Name,
            school.EmisNumber,
            school.AddressText,
            school.SchoolGateLocation.Latitude,
            school.SchoolGateLocation.Longitude,
            school.GateGeofenceRadiusMeters,
            school.AdminUserId);
    }

    public static DriverProfileDto ToDto(this DriverProfile driver, User user)
    {
        ArgumentNullException.ThrowIfNull(driver);
        ArgumentNullException.ThrowIfNull(user);

        return new DriverProfileDto(
            driver.Id,
            driver.UserId,
            user.FullName,
            user.Email,
            driver.FleetId,
            driver.ComplianceStatus.ToString(),
            driver.OperatingMode.ToString(),
            driver.PrdpNumber,
            driver.PrdpExpiryDate,
            driver.PoliceClearanceExpiryDate);
    }

    public static VehicleDto ToDto(this Vehicle vehicle)
    {
        ArgumentNullException.ThrowIfNull(vehicle);

        return new VehicleDto(
            vehicle.Id,
            vehicle.FleetId,
            vehicle.SchoolId,
            vehicle.AssignedDriverId,
            vehicle.RegistrationNumber,
            vehicle.MakeModel,
            vehicle.TotalSeatCapacity,
            vehicle.LicenseDiscExpiryDate,
            vehicle.CofExpiryDate,
            vehicle.IsActive);
    }

    public static ComplianceDocumentDto ToDto(this ComplianceDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return new ComplianceDocumentDto(
            document.Id,
            document.DriverProfileId,
            document.VehicleId,
            document.DocumentType.ToString(),
            document.DocumentNumber,
            document.StorageUrl,
            document.ExpiryDate,
            document.IsVerified,
            document.VerifiedAtUtc);
    }

    public static SchoolEndorsementDto ToDto(this SchoolEndorsement endorsement, string driverFullName)
    {
        ArgumentNullException.ThrowIfNull(endorsement);

        return new SchoolEndorsementDto(
            endorsement.Id,
            endorsement.SchoolId,
            endorsement.DriverProfileId,
            driverFullName,
            endorsement.Notes,
            endorsement.IsActive,
            endorsement.EndorsedAtUtc);
    }
}
