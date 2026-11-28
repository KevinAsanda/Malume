using Malume.Domain.Entities;
using Malume.Domain.Enums;
using Malume.Domain.Exceptions;
using Shouldly;

namespace Malume.Domain.UnitTests.Entities;

public sealed class ComplianceEntityTests
{
    [Fact]
    public void DriverProfile_UpdateDocumentExpiry_ShouldUpdateDriverDocsAndRejectVehicleTypes()
    {
        var driver = new DriverProfile(Guid.NewGuid(), "PRDP-1001");
        var prdpExpiry = new DateOnly(2028, 3, 1);
        var policeExpiry = new DateOnly(2027, 9, 15);

        driver.UpdateDocumentExpiry(ComplianceDocumentType.Prdp, prdpExpiry);
        driver.UpdateDocumentExpiry(ComplianceDocumentType.PoliceClearance, policeExpiry);

        driver.PrdpExpiryDate.ShouldBe(prdpExpiry);
        driver.PoliceClearanceExpiryDate.ShouldBe(policeExpiry);

        Should.Throw<DomainException>(() =>
            driver.UpdateDocumentExpiry(ComplianceDocumentType.LicenseDisc, new DateOnly(2028, 1, 1)));
    }

    [Fact]
    public void Vehicle_UpdateDocumentExpiry_ShouldRejectDriverDocumentType()
    {
        var vehicle = new Vehicle(
            "GP123ABC",
            "Toyota Quantum",
            15,
            new DateOnly(2027, 1, 1),
            new DateOnly(2027, 1, 1));

        Should.Throw<DomainException>(() =>
            vehicle.UpdateDocumentExpiry(ComplianceDocumentType.Prdp, new DateOnly(2028, 1, 1)));
    }

    [Fact]
    public void SchoolEndorsement_Reactivate_ShouldRestoreActiveStateAndUpdateMetadata()
    {
        Guid firstAdmin = Guid.NewGuid();
        Guid secondAdmin = Guid.NewGuid();
        var endorsement = new SchoolEndorsement(Guid.NewGuid(), Guid.NewGuid(), firstAdmin, "Initial approval");
        endorsement.Revoke();
        endorsement.IsActive.ShouldBeFalse();

        endorsement.Reactivate(secondAdmin, "Re-verified by SGB");

        endorsement.IsActive.ShouldBeTrue();
        endorsement.EndorsedByUserId.ShouldBe(secondAdmin);
        endorsement.Notes.ShouldBe("Re-verified by SGB");
    }
}
