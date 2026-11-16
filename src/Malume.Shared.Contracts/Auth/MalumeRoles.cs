namespace Malume.Shared.Contracts.Auth;

public static class MalumeRoles
{
    public const string Parent = "Parent";
    public const string Driver = "Driver";
    public const string FleetOwner = "FleetOwner";
    public const string SchoolAdmin = "SchoolAdmin";
    public const string SysAdmin = "SysAdmin";
}

public static class MalumeClaimTypes
{
    public const string FullName = "full_name";
    public const string TenantFleetId = "tenant_fleet_id";
    public const string TenantSchoolId = "tenant_school_id";
}
