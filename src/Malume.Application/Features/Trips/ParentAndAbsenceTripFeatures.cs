using Malume.Application.Abstractions.Identity;
using Malume.Application.Abstractions.Persistence;
using Malume.Application.Abstractions.Realtime;
using Malume.Application.Common;
using Malume.Application.Common.Exceptions;
using Malume.Domain.Common;
using Malume.Domain.Entities;
using Malume.Domain.Enums;
using Malume.Domain.ValueObjects;
using Malume.Shared.Contracts.Trips;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Malume.Application.Features.Trips;

public sealed record GetParentActiveTripsQuery(DateOnly? ServiceDate) : IRequest<IReadOnlyList<ParentActiveTripDto>>;

public sealed class GetParentActiveTripsQueryHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext,
    TimeProvider timeProvider) : IRequestHandler<GetParentActiveTripsQuery, IReadOnlyList<ParentActiveTripDto>>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<IReadOnlyList<ParentActiveTripDto>> Handle(
        GetParentActiveTripsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        User user = await CurrentUserGuard.RequireCurrentUserAsync(_currentUserService, _dbContext, cancellationToken);
        if (user.Role is not (UserRole.Parent or UserRole.SysAdmin))
        {
            throw new ForbiddenException("Only parents or system administrators can query parent active trips.");
        }

        IQueryable<Learner> learnerQuery = _dbContext.Learners.AsNoTracking();
        if (user.Role == UserRole.Parent)
        {
            learnerQuery = learnerQuery.Where(l => l.ParentUserId == user.Id);
        }

        Dictionary<Guid, Learner> learnersById = await learnerQuery.ToDictionaryAsync(l => l.Id, cancellationToken);
        if (learnersById.Count == 0)
        {
            return [];
        }

        DateOnly serviceDate = request.ServiceDate ?? SastClock.TodaySast(_timeProvider.GetUtcNow());
        List<Guid> learnerIds = learnersById.Keys.ToList();

        List<TripStopManifest> stops = await _dbContext.TripStopManifests
            .AsNoTracking()
            .Where(m => learnerIds.Contains(m.LearnerId))
            .ToListAsync(cancellationToken);

        if (stops.Count == 0)
        {
            return [];
        }

        List<Guid> tripIds = stops.Select(s => s.TripId).Distinct().ToList();
        Dictionary<Guid, DailyTripInstance> tripsById = await _dbContext.DailyTripInstances
            .AsNoTracking()
            .Where(t => tripIds.Contains(t.Id) && t.ServiceDate == serviceDate)
            .ToDictionaryAsync(t => t.Id, cancellationToken);

        if (tripsById.Count == 0)
        {
            return [];
        }

        List<Guid> routeIds = tripsById.Values.Select(t => t.RouteId).Distinct().ToList();
        Dictionary<Guid, ScheduledRoute> routesById = await _dbContext.ScheduledRoutes
            .AsNoTracking()
            .Where(r => routeIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, cancellationToken);

        List<Guid> schoolIds = routesById.Values.Select(r => r.SchoolId).Distinct().ToList();
        Dictionary<Guid, School> schoolsById = await _dbContext.Schools
            .AsNoTracking()
            .Where(s => schoolIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, cancellationToken);

        List<Guid> driverIds = tripsById.Values.Select(t => t.DriverProfileId).Distinct().ToList();
        Dictionary<Guid, DriverProfile> driversById = await _dbContext.DriverProfiles
            .AsNoTracking()
            .Where(d => driverIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, cancellationToken);

        List<Guid> driverUserIds = driversById.Values.Select(d => d.UserId).Distinct().ToList();
        Dictionary<Guid, User> driverUsersById = await _dbContext.Users
            .AsNoTracking()
            .Where(u => driverUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        List<Guid> vehicleIds = tripsById.Values.Select(t => t.VehicleId).Distinct().ToList();
        Dictionary<Guid, Vehicle> vehiclesById = await _dbContext.Vehicles
            .AsNoTracking()
            .Where(v => vehicleIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, cancellationToken);

        List<ParentActiveTripDto> result = [];
        foreach (TripStopManifest stop in stops)
        {
            if (!tripsById.TryGetValue(stop.TripId, out DailyTripInstance? trip))
            {
                continue;
            }

            Learner learner = learnersById[stop.LearnerId];
            ScheduledRoute route = routesById[trip.RouteId];
            School school = schoolsById[route.SchoolId];
            DriverProfile driver = driversById[trip.DriverProfileId];
            User driverUser = driverUsersById[driver.UserId];
            Vehicle vehicle = vehiclesById[trip.VehicleId];

            result.Add(new ParentActiveTripDto(
                trip.Id,
                route.Id,
                route.RouteName,
                route.Direction.ToString(),
                (int)route.Direction,
                trip.ServiceDate,
                trip.Status.ToString(),
                school.Id,
                school.Name,
                driverUser.FullName,
                vehicle.RegistrationNumber,
                learner.Id,
                learner.FullName,
                stop.AttendanceStatus.ToString(),
                stop.SequenceOrder,
                stop.EstimatedArrivalUtc,
                driver.CurrentLocation?.Latitude,
                driver.CurrentLocation?.Longitude,
                driver.LastLocationUpdatedAtUtc));
        }

        return result
            .OrderBy(t => t.DirectionCode)
            .ThenBy(t => t.SequenceOrder)
            .ToList();
    }
}

public sealed record MarkLearnerAbsentCommand(
    Guid TripId,
    Guid LearnerId) : IRequest<MarkLearnerAbsentResponse>;

public sealed class MarkLearnerAbsentCommandHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext,
    ITripRealtimeNotifier realtimeNotifier,
    TimeProvider timeProvider) : IRequestHandler<MarkLearnerAbsentCommand, MarkLearnerAbsentResponse>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;
    private readonly ITripRealtimeNotifier _realtimeNotifier = realtimeNotifier;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<MarkLearnerAbsentResponse> Handle(
        MarkLearnerAbsentCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        User user = await CurrentUserGuard.RequireCurrentUserAsync(_currentUserService, _dbContext, cancellationToken);

        DailyTripInstance trip = await _dbContext.DailyTripInstances
            .FirstOrDefaultAsync(t => t.Id == request.TripId, cancellationToken)
            ?? throw new NotFoundException($"Trip '{request.TripId}' was not found.");

        ScheduledRoute route = await _dbContext.ScheduledRoutes
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == trip.RouteId, cancellationToken)
            ?? throw new NotFoundException($"Scheduled route '{trip.RouteId}' was not found.");

        DriverProfile driver = await _dbContext.DriverProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == trip.DriverProfileId, cancellationToken)
            ?? throw new NotFoundException($"Driver profile '{trip.DriverProfileId}' was not found.");

        List<TripStopManifest> allStops = await _dbContext.TripStopManifests
            .Where(m => m.TripId == trip.Id)
            .OrderBy(m => m.SequenceOrder)
            .ToListAsync(cancellationToken);

        TripStopManifest targetStop = allStops.FirstOrDefault(s => s.LearnerId == request.LearnerId)
            ?? throw new NotFoundException($"Learner '{request.LearnerId}' is not on this trip manifest.");

        List<Guid> learnerIds = allStops.Select(s => s.LearnerId).Distinct().ToList();
        Dictionary<Guid, Learner> learnersById = await _dbContext.Learners
            .AsNoTracking()
            .Where(l => learnerIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, cancellationToken);

        Learner targetLearner = learnersById[targetStop.LearnerId];

        bool authorized = user.Role switch
        {
            UserRole.SysAdmin => true,
            UserRole.Parent => targetLearner.ParentUserId == user.Id,
            UserRole.SchoolAdmin => user.TenantSchoolId == route.SchoolId,
            _ => false
        };

        if (!authorized)
        {
            throw new ForbiddenException("You do not have permission to mark this learner absent.");
        }

        if (targetStop.AttendanceStatus == AttendanceStatus.MarkedAbsent)
        {
            return new MarkLearnerAbsentResponse(trip.Id, targetLearner.Id, trip.AvailableCorridorSeats, []);
        }

        GeoCoordinate origin = driver.CurrentLocation ?? route.CorridorPath.Points[0];
        DateTimeOffset departureAnchor = trip.StartedAtUtc ?? _timeProvider.GetUtcNow();

        RouteOptimizationService.EnsureBaselineEtas(origin, departureAnchor, allStops);

        targetStop.MarkAbsent();
        trip.IncrementAvailableCorridorSeat();

        IReadOnlyList<EtaCascadeUpdateDto> cascadeUpdates = RouteOptimizationService.RecomputeDownstreamEtas(
            origin,
            departureAnchor,
            allStops,
            learnersById);

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _realtimeNotifier.NotifyManifestMutatedAsync(
            trip.Id,
            targetLearner.Id,
            "LearnerMarkedAbsent",
            trip.AvailableCorridorSeats,
            cancellationToken);

        if (cascadeUpdates.Count > 0)
        {
            await _realtimeNotifier.NotifyEtaCascadeAsync(trip.Id, cascadeUpdates, cancellationToken);
        }

        return new MarkLearnerAbsentResponse(
            trip.Id,
            targetLearner.Id,
            trip.AvailableCorridorSeats,
            cascadeUpdates);
    }
}
