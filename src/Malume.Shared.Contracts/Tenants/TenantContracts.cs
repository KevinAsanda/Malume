namespace Malume.Shared.Contracts.Tenants;

public sealed record CreateFleetRequest(
    string Name,
    string? CompanyRegistrationNumber);

public sealed record FleetDto(
    Guid Id,
    string Name,
    string? CompanyRegistrationNumber,
    string? PaystackSubaccountCode,
    Guid OwnerUserId,
    bool IsActive);

public sealed record CreateSchoolRequest(
    string Name,
    string AddressText,
    double GateLatitude,
    double GateLongitude,
    string? EmisNumber,
    int GateGeofenceRadiusMeters = 150);

public sealed record SchoolDto(
    Guid Id,
    string Name,
    string? EmisNumber,
    string AddressText,
    double GateLatitude,
    double GateLongitude,
    int GateGeofenceRadiusMeters,
    Guid? AdminUserId);

public sealed record HireDriverRequest(string DriverEmail);
