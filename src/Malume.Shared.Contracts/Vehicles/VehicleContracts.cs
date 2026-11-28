namespace Malume.Shared.Contracts.Vehicles;

public sealed record CreateVehicleRequest(
    string RegistrationNumber,
    string MakeModel,
    int TotalSeatCapacity,
    DateOnly LicenseDiscExpiryDate,
    DateOnly CofExpiryDate);

public sealed record VehicleDto(
    Guid Id,
    Guid? FleetId,
    Guid? SchoolId,
    Guid? AssignedDriverId,
    string RegistrationNumber,
    string MakeModel,
    int TotalSeatCapacity,
    DateOnly LicenseDiscExpiryDate,
    DateOnly CofExpiryDate,
    bool IsActive);

public sealed record AssignVehicleDriverRequest(Guid? DriverProfileId);
