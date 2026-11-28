using Malume.Application.Abstractions.Persistence;
using Malume.Shared.Contracts.Corridor;
using Microsoft.EntityFrameworkCore;

namespace Malume.Infrastructure.Persistence.Queries;

internal sealed class SpatialCorridorQueryService(MalumeDbContext dbContext) : ISpatialCorridorQueryService
{
    private readonly MalumeDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<CorridorAdHocOptionDto>> FindCorridorOptionsAsync(
        Guid targetSchoolId,
        DateOnly serviceDate,
        double pickupLatitude,
        double pickupLongitude,
        double maxDistanceMeters,
        string? direction,
        CancellationToken cancellationToken)
    {
        string? normalizedDirection = string.IsNullOrWhiteSpace(direction) ? null : direction.Trim();

        List<CorridorAdHocOptionDto> results = await _dbContext.Database
            .SqlQuery<CorridorAdHocOptionDto>(
                $"""
                SELECT
                    t.TripId,
                    r.RouteId,
                    r.RouteName,
                    r.Direction,
                    t.ServiceDate,
                    t.DriverProfileId,
                    u.FullName AS DriverFullName,
                    v.MakeModel AS VehicleMakeModel,
                    v.RegistrationNumber AS VehicleRegistration,
                    t.AvailableCorridorSeats,
                    r.CorridorPath.STDistance(geography::Point({pickupLatitude}, {pickupLongitude}, 4326)) AS DistanceMeters,
                    CAST(
                        CASE WHEN EXISTS (
                            SELECT 1
                            FROM dbo.SchoolEndorsements e
                            WHERE e.SchoolId = r.SchoolId
                              AND e.DriverProfileId = t.DriverProfileId
                              AND e.IsActive = 1
                        ) THEN 1 ELSE 0 END
                    AS bit) AS HasSgbEndorsement
                FROM dbo.DailyTripInstances t
                INNER JOIN dbo.ScheduledRoutes r ON r.RouteId = t.RouteId
                INNER JOIN dbo.DriverProfiles d ON d.DriverId = t.DriverProfileId
                INNER JOIN dbo.Users u ON u.Id = d.UserId
                INNER JOIN dbo.Vehicles v ON v.VehicleId = t.VehicleId
                WHERE t.ServiceDate = {serviceDate}
                  AND r.SchoolId = {targetSchoolId}
                  AND r.IsActive = 1
                  AND v.IsActive = 1
                  AND u.IsActive = 1
                  AND t.Status IN ('Scheduled', 'EnRoute')
                  AND d.ComplianceStatus IN ('ActiveCompliant', 'ActiveExpiringSoon')
                  AND t.AvailableCorridorSeats > 0
                  AND ({normalizedDirection} IS NULL OR r.Direction = {normalizedDirection})
                  AND r.CorridorPath.STDistance(geography::Point({pickupLatitude}, {pickupLongitude}, 4326)) <= {maxDistanceMeters}
                ORDER BY DistanceMeters ASC
                """)
            .ToListAsync(cancellationToken);

        return results;
    }

    public async Task<double?> GetDistanceToRouteCorridorMetersAsync(
        Guid routeId,
        double pickupLatitude,
        double pickupLongitude,
        CancellationToken cancellationToken)
    {
        List<double> distances = await _dbContext.Database
            .SqlQuery<double>(
                $"""
                SELECT r.CorridorPath.STDistance(geography::Point({pickupLatitude}, {pickupLongitude}, 4326)) AS Value
                FROM dbo.ScheduledRoutes r
                WHERE r.RouteId = {routeId}
                """)
            .ToListAsync(cancellationToken);

        return distances.Count == 0 ? null : distances[0];
    }

    public async Task<IReadOnlyList<OffPeakDriverOptionDto>> FindNearbyOffPeakDriversAsync(
        double pickupLatitude,
        double pickupLongitude,
        double maxDistanceMeters,
        CancellationToken cancellationToken)
    {
        List<OffPeakDriverOptionDto> results = await _dbContext.Database
            .SqlQuery<OffPeakDriverOptionDto>(
                $"""
                SELECT
                    d.DriverId AS DriverProfileId,
                    u.FullName AS DriverFullName,
                    v.VehicleId,
                    v.MakeModel AS VehicleMakeModel,
                    v.RegistrationNumber AS VehicleRegistration,
                    d.CurrentLocation.STDistance(geography::Point({pickupLatitude}, {pickupLongitude}, 4326)) AS DistanceMeters
                FROM dbo.DriverProfiles d
                INNER JOIN dbo.Users u ON u.Id = d.UserId
                INNER JOIN dbo.Vehicles v ON v.AssignedDriverId = d.DriverId
                WHERE u.IsActive = 1
                  AND v.IsActive = 1
                  AND d.ComplianceStatus IN ('ActiveCompliant', 'ActiveExpiringSoon')
                  AND d.OperatingMode = 'OffPeakAdHoc'
                  AND d.CurrentLocation IS NOT NULL
                  AND d.CurrentLocation.STDistance(geography::Point({pickupLatitude}, {pickupLongitude}, 4326)) <= {maxDistanceMeters}
                ORDER BY DistanceMeters ASC
                """)
            .ToListAsync(cancellationToken);

        return results;
    }
}
