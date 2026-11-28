using Malume.Application.Abstractions.Identity;
using Malume.Application.Abstractions.Persistence;
using Malume.Application.Common;
using Malume.Application.Common.Exceptions;
using Malume.Domain.Common;
using Malume.Domain.Entities;
using Malume.Domain.Enums;
using Malume.Shared.Contracts.Trips;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Malume.Application.Features.Schools;

public sealed record GetSchoolGateBoardQuery(
    Guid SchoolId,
    DateOnly? ServiceDate) : IRequest<SchoolGateBoardDto>;

public sealed class GetSchoolGateBoardQueryHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext,
    TimeProvider timeProvider) : IRequestHandler<GetSchoolGateBoardQuery, SchoolGateBoardDto>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<SchoolGateBoardDto> Handle(
        GetSchoolGateBoardQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        User user = await CurrentUserGuard.RequireCurrentUserAsync(_currentUserService, _dbContext, cancellationToken);
        bool authorized = user.Role == UserRole.SysAdmin
            || (user.Role == UserRole.SchoolAdmin && user.TenantSchoolId == request.SchoolId);

        if (!authorized)
        {
            throw new ForbiddenException("Only the school's administrators or system administrators can view its live gate board.");
        }

        School school = await _dbContext.Schools
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.SchoolId, cancellationToken)
            ?? throw new NotFoundException($"School '{request.SchoolId}' was not found.");

        DateOnly serviceDate = request.ServiceDate ?? SastClock.TodaySast(_timeProvider.GetUtcNow());

        Dictionary<Guid, ScheduledRoute> routesById = await _dbContext.ScheduledRoutes
            .AsNoTracking()
            .Where(r => r.SchoolId == school.Id)
            .ToDictionaryAsync(r => r.Id, cancellationToken);

        if (routesById.Count == 0)
        {
            return new SchoolGateBoardDto(
                school.Id,
                school.Name,
                serviceDate,
                school.SchoolGateLocation.Latitude,
                school.SchoolGateLocation.Longitude,
                school.GateGeofenceRadiusMeters,
                []);
        }

        List<Guid> routeIds = routesById.Keys.ToList();
        List<DailyTripInstance> trips = await _dbContext.DailyTripInstances
            .AsNoTracking()
            .Where(t => routeIds.Contains(t.RouteId) && t.ServiceDate == serviceDate)
            .ToListAsync(cancellationToken);

        if (trips.Count == 0)
        {
            return new SchoolGateBoardDto(
                school.Id,
                school.Name,
                serviceDate,
                school.SchoolGateLocation.Latitude,
                school.SchoolGateLocation.Longitude,
                school.GateGeofenceRadiusMeters,
                []);
        }

        List<Guid> tripIds = trips.Select(t => t.Id).ToList();
        List<TripStopManifest> stops = await _dbContext.TripStopManifests
            .AsNoTracking()
            .Where(m => tripIds.Contains(m.TripId))
            .OrderBy(m => m.SequenceOrder)
            .ToListAsync(cancellationToken);

        List<Guid> learnerIds = stops.Select(s => s.LearnerId).Distinct().ToList();
        Dictionary<Guid, Learner> learnersById = learnerIds.Count == 0
            ? []
            : await _dbContext.Learners
                .AsNoTracking()
                .Where(l => learnerIds.Contains(l.Id))
                .ToDictionaryAsync(l => l.Id, cancellationToken);

        List<Guid> driverIds = trips.Select(t => t.DriverProfileId).Distinct().ToList();
        Dictionary<Guid, DriverProfile> driversById = await _dbContext.DriverProfiles
            .AsNoTracking()
            .Where(d => driverIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, cancellationToken);

        List<Guid> driverUserIds = driversById.Values.Select(d => d.UserId).Distinct().ToList();
        Dictionary<Guid, User> driverUsersById = await _dbContext.Users
            .AsNoTracking()
            .Where(u => driverUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        List<Guid> vehicleIds = trips.Select(t => t.VehicleId).Distinct().ToList();
        Dictionary<Guid, Vehicle> vehiclesById = await _dbContext.Vehicles
            .AsNoTracking()
            .Where(v => vehicleIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, cancellationToken);

        Dictionary<Guid, List<TripStopManifest>> stopsByTrip = stops
            .GroupBy(s => s.TripId)
            .ToDictionary(g => g.Key, g => g.ToList());

        List<GateArrivalVehicleDto> vehicleDtos = [];
        foreach (DailyTripInstance trip in trips)
        {
            ScheduledRoute route = routesById[trip.RouteId];
            DriverProfile driver = driversById[trip.DriverProfileId];
            User driverUser = driverUsersById[driver.UserId];
            Vehicle vehicle = vehiclesById[trip.VehicleId];
            List<TripStopManifest> tripStops = stopsByTrip.TryGetValue(trip.Id, out List<TripStopManifest>? found)
                ? found
                : [];

            double? distanceMeters = driver.CurrentLocation is null
                ? null
                : Math.Round(GeoMath.DistanceMeters(driver.CurrentLocation, school.SchoolGateLocation), 1);

            int? etaMinutes = distanceMeters.HasValue
                ? Math.Max(0, (int)Math.Ceiling(GeoMath.EstimateTravelMinutes(distanceMeters.Value)))
                : null;

            string zoneStatus = DetermineGateZoneStatus(
                trip.Status,
                distanceMeters,
                school.GateGeofenceRadiusMeters);

            int totalActive = tripStops.Count(s => s.AttendanceStatus != AttendanceStatus.MarkedAbsent);
            int boarded = tripStops.Count(s => s.AttendanceStatus is AttendanceStatus.PickedUp or AttendanceStatus.DroppedOff);

            List<GateLearnerSummaryDto> learnerSummaries = tripStops
                .Where(s => s.AttendanceStatus != AttendanceStatus.MarkedAbsent && learnersById.ContainsKey(s.LearnerId))
                .Select(s =>
                {
                    Learner learner = learnersById[s.LearnerId];
                    return new GateLearnerSummaryDto(
                        learner.Id,
                        learner.FullName,
                        learner.Grade,
                        s.AttendanceStatus.ToString());
                })
                .ToList();

            vehicleDtos.Add(new GateArrivalVehicleDto(
                trip.Id,
                route.Id,
                route.RouteName,
                vehicle.RegistrationNumber,
                driverUser.FullName,
                trip.Status.ToString(),
                zoneStatus,
                distanceMeters,
                etaMinutes,
                boarded,
                totalActive,
                trip.CompletedAtUtc,
                learnerSummaries));
        }

        return new SchoolGateBoardDto(
            school.Id,
            school.Name,
            serviceDate,
            school.SchoolGateLocation.Latitude,
            school.SchoolGateLocation.Longitude,
            school.GateGeofenceRadiusMeters,
            vehicleDtos
                .OrderBy(v => v.DistanceToGateMeters ?? double.MaxValue)
                .ThenBy(v => v.RouteName, StringComparer.Ordinal)
                .ToList());
    }

    private static string DetermineGateZoneStatus(
        TripLifecycleStatus tripStatus,
        double? distanceMeters,
        int gateGeofenceRadiusMeters)
    {
        if (tripStatus == TripLifecycleStatus.Completed)
        {
            return "Departed";
        }

        if (tripStatus == TripLifecycleStatus.Cancelled)
        {
            return "Cancelled";
        }

        if (tripStatus == TripLifecycleStatus.AtSchoolGate
            || (distanceMeters.HasValue && distanceMeters.Value <= gateGeofenceRadiusMeters))
        {
            return "AtPickupZone";
        }

        if (distanceMeters.HasValue && distanceMeters.Value <= GeoMath.ApproachingGateRadiusMeters)
        {
            return "Approaching";
        }

        return tripStatus == TripLifecycleStatus.EnRoute ? "EnRoute" : "Scheduled";
    }
}
