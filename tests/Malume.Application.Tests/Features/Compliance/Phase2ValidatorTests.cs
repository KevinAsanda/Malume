using Malume.Application.Features.Compliance;
using Malume.Application.Features.Schools;
using Malume.Application.Features.Vehicles;
using Shouldly;

namespace Malume.Application.Tests.Features.Compliance;

public sealed class Phase2ValidatorTests
{
    [Fact]
    public void SubmitComplianceDocumentValidator_WhenInvalidType_ShouldFail()
    {
        var validator = new SubmitComplianceDocumentCommandValidator();
        var command = new SubmitComplianceDocumentCommand(
            "DriversLicense",
            "DL-001",
            "https://storage.example/dl.pdf",
            new DateOnly(2028, 1, 1),
            null);

        validator.Validate(command).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void CreateSchoolValidator_WhenLatitudeOutOfRange_ShouldFail()
    {
        var validator = new CreateSchoolCommandValidator();
        var command = new CreateSchoolCommand(
            "Sandton Primary",
            "100 Rivonia Rd",
            -95.0,
            28.05,
            "EMIS-101");

        validator.Validate(command).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void RegisterVehicleValidator_WhenCapacityZero_ShouldFail()
    {
        var validator = new RegisterVehicleCommandValidator();
        var command = new RegisterVehicleCommand(
            "GP001XY",
            "Toyota Quantum",
            0,
            new DateOnly(2028, 1, 1),
            new DateOnly(2028, 1, 1));

        validator.Validate(command).IsValid.ShouldBeFalse();
    }
}
