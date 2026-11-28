using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Malume.Application.Abstractions.Identity;
using Malume.Application.Abstractions.Persistence;
using Malume.Domain.Entities;
using Malume.Domain.Enums;
using Malume.Shared.Contracts.Trips;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Malume.Api.Hubs;

[Authorize]
public sealed class TripTelemetryHub(
    IMalumeDbContext dbContext,
    ICurrentUserService currentUserService) : Hub
{
    private const double EarthRadiusMeters = 6_371_000.0;
    private const double ApproachingGateThresholdMeters = 2_000.0;

    private readonly IMalumeDbContext _dbContext = dbContext;
    private readonly ICurrentUserService _currentUserService = currentUserService;

    public static string TripGroup(Guid tripId) => $"trip:{tripId:D}";

    public static string SchoolGroup(Guid schoolId) => $"school:{schoolId:D}";

    public const string AdminSosGroup = "admin:sos";

    public async Task JoinTripGroup(Guid tripId)
    {
        User user = await RequireCurrentUserAsync();

        DailyTripInstance trip = await _dbContext.DailyTripInstances
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tripId, Context.ConnectionAborted)
            ?? throw new HubException($"Trip '{tripId}' was not found.");

        ScheduledRoute route = await _dbContext.ScheduledRoutes
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == trip.RouteId, Context.ConnectionAborted)
            ?? throw new HubException("Scheduled route was not found.");

        Vehicle vehicle = await _dbContext.Vehicles
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == trip.VehicleId, Context.ConnectionAborted)
            ?? throw new HubException("Vehicle was not found.");

        DriverProfile driver = await _dbContext.DriverProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == trip.DriverProfileId, Context.ConnectionAborted)
            ?? throw new HubException("Driver profile was not found.");

        bool authorized = user.Role switch
        {
            UserRole.SysAdmin => true,
            UserRole.Driver => driver.UserId == user.Id,
            UserRole.FleetOwner => vehicle.FleetId.HasValue && user.TenantFleetId == vehicle.FleetId.Value,
            UserRole.SchoolAdmin => user.TenantSchoolId == route.SchoolId,
            UserRole.Parent => await _dbContext.TripStopManifests
                .AsNoTracking()
                .AnyAsync(
                    m => m.TripId == trip.Id
                         && _dbContext.Learners.Any(l => l.Id == m.LearnerId && l.ParentUserId == user.Id),
                    Context.ConnectionAborted),
            _ => false
        };

        if (!authorized)
        {
            throw new HubException("You do not have permission to subscribe to this trip's telemetry.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, TripGroup(tripId), Context.ConnectionAborted);
    }

    public async Task LeaveTripGroup(Guid tripId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, TripGroup(tripId), Context.ConnectionAborted);
    }

    public async Task JoinSchoolGateGroup(Guid schoolId)
    {
        User user = await RequireCurrentUserAsync();
        bool authorized = user.Role == UserRole.SysAdmin
            || (user.Role == UserRole.SchoolAdmin && user.TenantSchoolId == schoolId);

        if (!authorized)
        {
            throw new HubException("Only school administrators for this school or system administrators can join the gate board.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, SchoolGroup(schoolId), Context.ConnectionAborted);
    }

    public async Task JoinAdminSosGroup()
    {
        User user = await RequireCurrentUserAsync();
        if (user.Role != UserRole.SysAdmin)
        {
            throw new HubException("Only system administrators can monitor the global SOS feed.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, AdminSosGroup, Context.ConnectionAborted);
    }

    public async Task StreamLocation(DriverTelemetryPingDto ping)
    {
        ArgumentNullException.ThrowIfNull(ping);

        User user = await RequireCurrentUserAsync();
        if (user.Role != UserRole.Driver)
        {
            throw new HubException("Only drivers can stream live trip telemetry.");
        }

        DriverProfile driver = await _dbContext.DriverProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.UserId == user.Id, Context.ConnectionAborted)
            ?? throw new HubException("Driver profile was not found.");

        DailyTripInstance trip = await _dbContext.DailyTripInstances
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == ping.TripId, Context.ConnectionAborted)
            ?? throw new HubException($"Trip '{ping.TripId}' was not found.");

        if (trip.DriverProfileId != driver.Id)
        {
            throw new HubException("You are not assigned to this trip.");
        }

        await Clients.Group(TripGroup(ping.TripId)).SendAsync("LocationUpdated", ping, Context.ConnectionAborted);

        ScheduledRoute? route = await _dbContext.ScheduledRoutes
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == trip.RouteId, Context.ConnectionAborted);

        if (route is not null)
        {
            School? school = await _dbContext.Schools
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == route.SchoolId, Context.ConnectionAborted);

            if (school is not null)
            {
                double distanceMeters = CalculateHaversineDistanceMeters(
                    ping.Latitude,
                    ping.Longitude,
                    school.SchoolGateLocation.Latitude,
                    school.SchoolGateLocation.Longitude);

                if (distanceMeters <= ApproachingGateThresholdMeters)
                {
                    await Clients.Group(SchoolGroup(school.Id)).SendAsync(
                        "SchoolGateApproaching",
                        new
                        {
                            ping.TripId,
                            SchoolId = school.Id,
                            DistanceMeters = Math.Round(distanceMeters, 1),
                            AtPickupZone = distanceMeters <= school.GateGeofenceRadiusMeters,
                            ping.RecordedAtUtc
                        },
                        Context.ConnectionAborted);
                }
            }
        }
    }

    private async Task<User> RequireCurrentUserAsync()
    {
        string? rawId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Context.User?.FindFirstValue(JwtRegisteredClaimNames.Sub);

        Guid? userId = Guid.TryParse(rawId, out Guid parsed)
            ? parsed
            : _currentUserService.UserId;

        if (userId is not Guid resolvedUserId)
        {
            throw new HubException("Authenticated user identity claim is missing.");
        }

        User? user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == resolvedUserId, Context.ConnectionAborted);

        if (user is null || !user.IsActive)
        {
            throw new HubException("Authenticated user account was not found or is inactive.");
        }

        return user;
    }

    private static double CalculateHaversineDistanceMeters(
        double lat1,
        double lon1,
        double lat2,
        double lon2)
    {
        double lat1Rad = DegreesToRadians(lat1);
        double lat2Rad = DegreesToRadians(lat2);
        double deltaLat = DegreesToRadians(lat2 - lat1);
        double deltaLon = DegreesToRadians(lon2 - lon1);

        double a = Math.Sin(deltaLat / 2.0) * Math.Sin(deltaLat / 2.0)
            + Math.Cos(lat1Rad) * Math.Cos(lat2Rad) * Math.Sin(deltaLon / 2.0) * Math.Sin(deltaLon / 2.0);
        double c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
        return EarthRadiusMeters * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * (Math.PI / 180.0);
}
