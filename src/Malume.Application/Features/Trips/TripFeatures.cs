using FluentValidation;
using Malume.Application.Abstractions.Identity;
using Malume.Application.Abstractions.Persistence;
using Malume.Application.Abstractions.Realtime;
using Malume.Application.Abstractions.Security;
using Malume.Application.Common;
using Malume.Application.Common.Exceptions;
using Malume.Domain.Common;
using Malume.Domain.Entities;
using Malume.Domain.Enums;
using Malume.Domain.Exceptions;
using Malume.Domain.ValueObjects;
using Malume.Shared.Contracts.Trips;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Malume.Application.Features.Trips;

public sealed record CreateDailyTripCommand(
    Guid RouteId,
    DateOnly ServiceDate) : IRequest<TripManifestDto>;

public sealed class CreateDailyTripCommandValidator : AbstractValidator<CreateDailyTripCommand>
{
    public CreateDailyTripCommandValidator()
    {
        RuleFor(x => x.RouteId).NotEmpty();
    }
}

public sealed class CreateDailyTripCommandHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext,
    IQrBadgeSigner qrBadgeSigner) : IRequestHandler<CreateDailyTripCommand, TripManifestDto>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;
    private readonly IQrBadgeSigner _qrBadgeSigner = qrBadgeSigner;

    public async Task<TripManifestDto> Handle(CreateDailyTripCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        User user = await CurrentUserGuard.RequireCurrentUserAsync(_currentUserService, _dbContext, cancellationToken);

        ScheduledRoute route = await _dbContext.ScheduledRoutes
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.RouteId, cancellationToken)
            ?? throw new NotFoundException($"Scheduled route '{request.RouteId}' was not found.");

        Vehicle vehicle = await _dbContext.Vehicles
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == route.VehicleId, cancellationToken)
            ?? throw new NotFoundException($"Vehicle '{route.VehicleId}' was not found.");

        School school = await _dbContext.Schools
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == route.SchoolId, cancellationToken)
            ?? throw new NotFoundException($"School '{route.SchoolId}' was not found.");

        DriverProfile driver = await _dbContext.DriverProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == route.DriverProfileId, cancellationToken)
            ?? throw new NotFoundException($"Driver profile '{route.DriverProfileId}' was not found.");

        bool authorized = user.Role switch
        {
            UserRole.SysAdmin => true,
            UserRole.Driver => driver.UserId == user.Id,
            UserRole.FleetOwner => vehicle.FleetId.HasValue && user.TenantFleetId == vehicle.FleetId.Value,
            UserRole.SchoolAdmin => user.TenantSchoolId == route.SchoolId,
            _ => false
        };

        if (!authorized)
        {
            throw new ForbiddenException("You are not authorized to generate daily trips for this route.");
        }

        bool duplicateExists = await _dbContext.DailyTripInstances.AnyAsync(
            t => t.RouteId == route.Id && t.ServiceDate == request.ServiceDate,
            cancellationToken);

        if (duplicateExists)
        {
            throw new ConflictException($"A daily trip already exists for route '{route.RouteName}' on {request.ServiceDate:yyyy-MM-dd}.");
        }

        string billingYearMonth = request.ServiceDate.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);

        List<Guid> subscribedLearnerIds = await _dbContext.Subscriptions
            .AsNoTracking()
            .Where(s => s.RouteId == route.Id
                        && s.EscrowStatus == EscrowStatus.Active
                        && s.BillingYearMonth == billingYearMonth)
            .Select(s => s.LearnerId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (subscribedLearnerIds.Count == 0)
        {
            subscribedLearnerIds = await _dbContext.Subscriptions
                .AsNoTracking()
                .Where(s => s.RouteId == route.Id && s.EscrowStatus == EscrowStatus.Active)
                .Select(s => s.LearnerId)
                .Distinct()
                .ToListAsync(cancellationToken);
        }

        List<Learner> learners = subscribedLearnerIds.Count == 0
            ? []
            : await _dbContext.Learners
                .AsNoTracking()
                .Where(l => subscribedLearnerIds.Contains(l.Id))
                .OrderBy(l => l.FullName)
                .ThenBy(l => l.Id)
                .ToListAsync(cancellationToken);

        int availableSeats = Math.Max(0, vehicle.TotalSeatCapacity - learners.Count);
        var trip = new DailyTripInstance(
            route.Id,
            route.DriverProfileId,
            route.VehicleId,
            request.ServiceDate,
            availableSeats);

        await _dbContext.DailyTripInstances.AddAsync(trip, cancellationToken);

        List<TripStopManifest> stops = [];
        for (int index = 0; index < learners.Count; index++)
        {
            Learner learner = learners[index];
            var stop = new TripStopManifest(
                trip.Id,
                learner.Id,
                BookingType.MonthlyContract,
                index + 1,
                learner.HomePickupPoint);
            stops.Add(stop);
            await _dbContext.TripStopManifests.AddAsync(stop, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return TripManifestMapper.BuildManifestDto(
            trip,
            route,
            school,
            vehicle,
            driver,
            stops,
            learners.ToDictionary(l => l.Id),
            _qrBadgeSigner.PublicKeyBase64);
    }
}

public sealed record GetDriverScheduleQuery(DateOnly? ServiceDate) : IRequest<IReadOnlyList<DriverTripSummaryDto>>;

public sealed class GetDriverScheduleQueryHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext,
    TimeProvider timeProvider) : IRequestHandler<GetDriverScheduleQuery, IReadOnlyList<DriverTripSummaryDto>>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<IReadOnlyList<DriverTripSummaryDto>> Handle(
        GetDriverScheduleQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        User user = await CurrentUserGuard.RequireCurrentUserAsync(_currentUserService, _dbContext, cancellationToken);
        if (user.Role != UserRole.Driver)
        {
            throw new ForbiddenException("Only drivers can access the driver trip schedule.");
        }

        DriverProfile driver = await _dbContext.DriverProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.UserId == user.Id, cancellationToken)
            ?? throw new NotFoundException("Driver profile was not found.");

        DateOnly serviceDate = request.ServiceDate ?? SastClock.TodaySast(_timeProvider.GetUtcNow());

        List<DailyTripInstance> trips = await _dbContext.DailyTripInstances
            .AsNoTracking()
            .Where(t => t.DriverProfileId == driver.Id && t.ServiceDate == serviceDate)
            .ToListAsync(cancellationToken);

        if (trips.Count == 0)
        {
            return [];
        }

        List<Guid> routeIds = trips.Select(t => t.RouteId).Distinct().ToList();
        Dictionary<Guid, ScheduledRoute> routesById = await _dbContext.ScheduledRoutes
            .AsNoTracking()
            .Where(r => routeIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, cancellationToken);

        List<Guid> schoolIds = routesById.Values.Select(r => r.SchoolId).Distinct().ToList();
        Dictionary<Guid, School> schoolsById = await _dbContext.Schools
            .AsNoTracking()
            .Where(s => schoolIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, cancellationToken);

        List<Guid> vehicleIds = trips.Select(t => t.VehicleId).Distinct().ToList();
        Dictionary<Guid, Vehicle> vehiclesById = await _dbContext.Vehicles
            .AsNoTracking()
            .Where(v => vehicleIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, cancellationToken);

        List<Guid> tripIds = trips.Select(t => t.Id).ToList();
        List<TripStopManifest> manifests = await _dbContext.TripStopManifests
            .AsNoTracking()
            .Where(m => tripIds.Contains(m.TripId))
            .ToListAsync(cancellationToken);

        Dictionary<Guid, List<TripStopManifest>> manifestsByTrip = manifests
            .GroupBy(m => m.TripId)
            .ToDictionary(g => g.Key, g => g.ToList());

        List<DriverTripSummaryDto> summaries = [];
        foreach (DailyTripInstance trip in trips)
        {
            ScheduledRoute route = routesById[trip.RouteId];
            School school = schoolsById[route.SchoolId];
            Vehicle vehicle = vehiclesById[trip.VehicleId];
            List<TripStopManifest> tripStops = manifestsByTrip.TryGetValue(trip.Id, out List<TripStopManifest>? found)
                ? found
                : [];
            int completedStops = tripStops.Count(s => s.AttendanceStatus is AttendanceStatus.PickedUp or AttendanceStatus.DroppedOff);

            summaries.Add(new DriverTripSummaryDto(
                trip.Id,
                route.Id,
                route.RouteName,
                route.Direction.ToString(),
                (int)route.Direction,
                school.Id,
                school.Name,
                vehicle.Id,
                vehicle.RegistrationNumber,
                trip.ServiceDate,
                trip.Status.ToString(),
                trip.AvailableCorridorSeats,
                tripStops.Count,
                completedStops,
                trip.StartedAtUtc,
                trip.ArrivedAtSchoolGateUtc,
                trip.CompletedAtUtc));
        }

        return summaries
            .OrderBy(s => s.DirectionCode)
            .ThenBy(s => s.RouteName, StringComparer.Ordinal)
            .ToList();
    }
}

public sealed record GetTripManifestQuery(Guid TripId) : IRequest<TripManifestDto>;

public sealed class GetTripManifestQueryHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext,
    IQrBadgeSigner qrBadgeSigner) : IRequestHandler<GetTripManifestQuery, TripManifestDto>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;
    private readonly IQrBadgeSigner _qrBadgeSigner = qrBadgeSigner;

    public async Task<TripManifestDto> Handle(GetTripManifestQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        User user = await CurrentUserGuard.RequireCurrentUserAsync(_currentUserService, _dbContext, cancellationToken);

        DailyTripInstance trip = await _dbContext.DailyTripInstances
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.TripId, cancellationToken)
            ?? throw new NotFoundException($"Trip '{request.TripId}' was not found.");

        ScheduledRoute route = await _dbContext.ScheduledRoutes
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == trip.RouteId, cancellationToken)
            ?? throw new NotFoundException($"Scheduled route '{trip.RouteId}' was not found.");

        School school = await _dbContext.Schools
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == route.SchoolId, cancellationToken)
            ?? throw new NotFoundException($"School '{route.SchoolId}' was not found.");

        Vehicle vehicle = await _dbContext.Vehicles
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == trip.VehicleId, cancellationToken)
            ?? throw new NotFoundException($"Vehicle '{trip.VehicleId}' was not found.");

        DriverProfile driver = await _dbContext.DriverProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == trip.DriverProfileId, cancellationToken)
            ?? throw new NotFoundException($"Driver profile '{trip.DriverProfileId}' was not found.");

        bool authorized = user.Role switch
        {
            UserRole.SysAdmin => true,
            UserRole.Driver => driver.UserId == user.Id,
            UserRole.FleetOwner => vehicle.FleetId.HasValue && user.TenantFleetId == vehicle.FleetId.Value,
            UserRole.SchoolAdmin => user.TenantSchoolId == school.Id,
            _ => false
        };

        if (!authorized)
        {
            throw new ForbiddenException("You do not have permission to access this trip manifest.");
        }

        List<TripStopManifest> stops = await _dbContext.TripStopManifests
            .AsNoTracking()
            .Where(m => m.TripId == trip.Id)
            .OrderBy(m => m.SequenceOrder)
            .ToListAsync(cancellationToken);

        List<Guid> learnerIds = stops.Select(s => s.LearnerId).Distinct().ToList();
        Dictionary<Guid, Learner> learnersById = learnerIds.Count == 0
            ? []
            : await _dbContext.Learners
                .AsNoTracking()
                .Where(l => learnerIds.Contains(l.Id))
                .ToDictionaryAsync(l => l.Id, cancellationToken);

        return TripManifestMapper.BuildManifestDto(
            trip,
            route,
            school,
            vehicle,
            driver,
            stops,
            learnersById,
            _qrBadgeSigner.PublicKeyBase64);
    }
}

public sealed record StartTripCommand(Guid TripId) : IRequest<DriverTripSummaryDto>;

public sealed class StartTripCommandHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext,
    ITripRealtimeNotifier realtimeNotifier,
    TimeProvider timeProvider) : IRequestHandler<StartTripCommand, DriverTripSummaryDto>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;
    private readonly ITripRealtimeNotifier _realtimeNotifier = realtimeNotifier;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<DriverTripSummaryDto> Handle(StartTripCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        (DailyTripInstance trip, DriverProfile driver, ScheduledRoute route, School school, Vehicle vehicle) =
            await TripLifecycleLoader.LoadOwnedTripAsync(_currentUserService, _dbContext, request.TripId, cancellationToken);

        trip.StartTrip(driver.ComplianceStatus);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _realtimeNotifier.NotifyTripStatusChangedAsync(
            trip.Id,
            trip.Status.ToString(),
            trip.StartedAtUtc ?? _timeProvider.GetUtcNow(),
            cancellationToken);

        return await TripLifecycleLoader.BuildSummaryAsync(_dbContext, trip, route, school, vehicle, cancellationToken);
    }
}

public sealed record ArriveAtSchoolGateCommand(Guid TripId) : IRequest<DriverTripSummaryDto>;

public sealed class ArriveAtSchoolGateCommandHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext,
    ITripRealtimeNotifier realtimeNotifier,
    TimeProvider timeProvider) : IRequestHandler<ArriveAtSchoolGateCommand, DriverTripSummaryDto>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;
    private readonly ITripRealtimeNotifier _realtimeNotifier = realtimeNotifier;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<DriverTripSummaryDto> Handle(ArriveAtSchoolGateCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        (DailyTripInstance trip, _, ScheduledRoute route, School school, Vehicle vehicle) =
            await TripLifecycleLoader.LoadOwnedTripAsync(_currentUserService, _dbContext, request.TripId, cancellationToken);

        trip.ArriveAtSchoolGate();
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _realtimeNotifier.NotifyTripStatusChangedAsync(
            trip.Id,
            trip.Status.ToString(),
            trip.ArrivedAtSchoolGateUtc ?? _timeProvider.GetUtcNow(),
            cancellationToken);

        return await TripLifecycleLoader.BuildSummaryAsync(_dbContext, trip, route, school, vehicle, cancellationToken);
    }
}

public sealed record CompleteTripCommand(Guid TripId) : IRequest<DriverTripSummaryDto>;

public sealed class CompleteTripCommandHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext,
    ITripRealtimeNotifier realtimeNotifier,
    TimeProvider timeProvider) : IRequestHandler<CompleteTripCommand, DriverTripSummaryDto>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;
    private readonly ITripRealtimeNotifier _realtimeNotifier = realtimeNotifier;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<DriverTripSummaryDto> Handle(CompleteTripCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        (DailyTripInstance trip, _, ScheduledRoute route, School school, Vehicle vehicle) =
            await TripLifecycleLoader.LoadOwnedTripAsync(_currentUserService, _dbContext, request.TripId, cancellationToken);

        trip.CompleteTrip();
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _realtimeNotifier.NotifyTripStatusChangedAsync(
            trip.Id,
            trip.Status.ToString(),
            trip.CompletedAtUtc ?? _timeProvider.GetUtcNow(),
            cancellationToken);

        return await TripLifecycleLoader.BuildSummaryAsync(_dbContext, trip, route, school, vehicle, cancellationToken);
    }
}

public sealed record BatchSyncHandoversCommand(
    Guid TripId,
    IReadOnlyList<HandoverEventItemRequest> Events) : IRequest<BatchSyncHandoversResponse>;

public sealed class BatchSyncHandoversCommandValidator : AbstractValidator<BatchSyncHandoversCommand>
{
    public BatchSyncHandoversCommandValidator()
    {
        RuleFor(x => x.TripId).NotEmpty();
        RuleFor(x => x.Events).NotNull();
        RuleForEach(x => x.Events).ChildRules(ev =>
        {
            ev.RuleFor(e => e.EventId).NotEmpty();
            ev.RuleFor(e => e.LearnerId).NotEmpty();
            ev.RuleFor(e => e.Action)
                .NotEmpty()
                .Must(a => Enum.TryParse<AttendanceStatus>(a, ignoreCase: true, out AttendanceStatus parsed)
                           && parsed is AttendanceStatus.PickedUp or AttendanceStatus.DroppedOff)
                .WithMessage("Handover Action must be PickedUp or DroppedOff.");
            ev.RuleFor(e => e.Method)
                .NotEmpty()
                .Must(m => Enum.TryParse<HandoverMethod>(m, ignoreCase: true, out HandoverMethod parsed)
                           && parsed is HandoverMethod.QrScan or HandoverMethod.FallbackPin)
                .WithMessage("Handover Method must be QrScan or FallbackPin.");
            ev.RuleFor(e => e.Latitude).InclusiveBetween(-90.0, 90.0);
            ev.RuleFor(e => e.Longitude).InclusiveBetween(-180.0, 180.0);
        });
    }
}

public sealed class BatchSyncHandoversCommandHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext,
    IQrBadgeSigner qrBadgeSigner,
    ITripRealtimeNotifier realtimeNotifier) : IRequestHandler<BatchSyncHandoversCommand, BatchSyncHandoversResponse>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;
    private readonly IQrBadgeSigner _qrBadgeSigner = qrBadgeSigner;
    private readonly ITripRealtimeNotifier _realtimeNotifier = realtimeNotifier;

    public async Task<BatchSyncHandoversResponse> Handle(
        BatchSyncHandoversCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        (DailyTripInstance trip, _, ScheduledRoute route, _, _) =
            await TripLifecycleLoader.LoadOwnedTripAsync(_currentUserService, _dbContext, request.TripId, cancellationToken);

        List<TripStopManifest> stops = await _dbContext.TripStopManifests
            .Where(m => m.TripId == trip.Id)
            .ToListAsync(cancellationToken);

        Dictionary<Guid, TripStopManifest> stopsByLearner = stops.ToDictionary(s => s.LearnerId);

        List<Guid> learnerIds = stops.Select(s => s.LearnerId).Distinct().ToList();
        Dictionary<Guid, Learner> learnersById = learnerIds.Count == 0
            ? []
            : await _dbContext.Learners
                .AsNoTracking()
                .Where(l => learnerIds.Contains(l.Id))
                .ToDictionaryAsync(l => l.Id, cancellationToken);

        List<Guid> incomingEventIds = request.Events.Select(e => e.EventId).Distinct().ToList();
        HashSet<Guid> knownEventIds = incomingEventIds.Count == 0
            ? []
            : (await _dbContext.TripStopManifests
                .AsNoTracking()
                .Where(m => m.OfflineEventId != null && incomingEventIds.Contains(m.OfflineEventId.Value))
                .Select(m => m.OfflineEventId!.Value)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        List<HandoverSyncItemResultDto> results = [];
        List<(Guid LearnerId, AttendanceStatus Status, HandoverMethod Method, DateTimeOffset TimestampUtc)> appliedNotifications = [];

        foreach (HandoverEventItemRequest ev in request.Events.OrderBy(e => e.RecordedAtUtc))
        {
            if (knownEventIds.Contains(ev.EventId))
            {
                results.Add(new HandoverSyncItemResultDto(ev.EventId, ev.LearnerId, "Duplicate", null));
                continue;
            }

            if (!Enum.TryParse<AttendanceStatus>(ev.Action, ignoreCase: true, out AttendanceStatus action)
                || action is not (AttendanceStatus.PickedUp or AttendanceStatus.DroppedOff)
                || !Enum.TryParse<HandoverMethod>(ev.Method, ignoreCase: true, out HandoverMethod method)
                || method == HandoverMethod.None)
            {
                results.Add(new HandoverSyncItemResultDto(ev.EventId, ev.LearnerId, "Rejected", "Invalid handover action or method."));
                continue;
            }

            if (!stopsByLearner.TryGetValue(ev.LearnerId, out TripStopManifest? manifest)
                || !learnersById.TryGetValue(ev.LearnerId, out Learner? learner))
            {
                results.Add(new HandoverSyncItemResultDto(ev.EventId, ev.LearnerId, "Rejected", "Learner is not on this trip manifest."));
                continue;
            }

            if (manifest.OfflineEventId == ev.EventId
                || (action == AttendanceStatus.PickedUp && manifest.AttendanceStatus is AttendanceStatus.PickedUp or AttendanceStatus.DroppedOff)
                || (action == AttendanceStatus.DroppedOff && manifest.AttendanceStatus == AttendanceStatus.DroppedOff))
            {
                results.Add(new HandoverSyncItemResultDto(ev.EventId, ev.LearnerId, "Duplicate", null));
                continue;
            }

            if (manifest.AttendanceStatus == AttendanceStatus.MarkedAbsent)
            {
                results.Add(new HandoverSyncItemResultDto(ev.EventId, ev.LearnerId, "Rejected", "Learner was marked absent."));
                continue;
            }

            if (method == HandoverMethod.QrScan
                && !string.IsNullOrWhiteSpace(ev.QrPayload)
                && !_qrBadgeSigner.VerifyLearnerBadge(ev.QrPayload, learner.Id, learner.SchoolId))
            {
                results.Add(new HandoverSyncItemResultDto(ev.EventId, ev.LearnerId, "Rejected", "Invalid learner QR signature."));
                continue;
            }

            if (method == HandoverMethod.FallbackPin
                && !string.IsNullOrWhiteSpace(ev.FallbackPin)
                && !learner.VerifyDailyPin(trip.ServiceDate, route.Direction, ev.FallbackPin))
            {
                results.Add(new HandoverSyncItemResultDto(ev.EventId, ev.LearnerId, "Rejected", "Invalid daily rotating PIN."));
                continue;
            }

            manifest.RecordHandover(
                action,
                method,
                ev.RecordedAtUtc,
                new GeoCoordinate(ev.Latitude, ev.Longitude),
                ev.WasSyncedOffline,
                ev.EventId);

            knownEventIds.Add(ev.EventId);
            results.Add(new HandoverSyncItemResultDto(ev.EventId, ev.LearnerId, "Applied", null));
            appliedNotifications.Add((ev.LearnerId, action, method, ev.RecordedAtUtc));
        }

        if (appliedNotifications.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);

            foreach ((Guid learnerId, AttendanceStatus status, HandoverMethod method, DateTimeOffset timestampUtc) in appliedNotifications)
            {
                await _realtimeNotifier.NotifyHandoverRecordedAsync(
                    trip.Id,
                    learnerId,
                    status.ToString(),
                    method.ToString(),
                    timestampUtc,
                    cancellationToken);
            }
        }

        return new BatchSyncHandoversResponse(
            trip.Id,
            results.Count(r => string.Equals(r.Outcome, "Applied", StringComparison.Ordinal)),
            results.Count(r => string.Equals(r.Outcome, "Duplicate", StringComparison.Ordinal)),
            results.Count(r => string.Equals(r.Outcome, "Rejected", StringComparison.Ordinal)),
            results);
    }
}

public sealed record TriggerSosCommand(
    Guid TripId,
    double Latitude,
    double Longitude,
    string? Notes) : IRequest<SosAlertDto>;

public sealed class TriggerSosCommandValidator : AbstractValidator<TriggerSosCommand>
{
    public TriggerSosCommandValidator()
    {
        RuleFor(x => x.TripId).NotEmpty();
        RuleFor(x => x.Latitude).InclusiveBetween(-90.0, 90.0);
        RuleFor(x => x.Longitude).InclusiveBetween(-180.0, 180.0);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public sealed class TriggerSosCommandHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext,
    ITripRealtimeNotifier realtimeNotifier,
    TimeProvider timeProvider) : IRequestHandler<TriggerSosCommand, SosAlertDto>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;
    private readonly ITripRealtimeNotifier _realtimeNotifier = realtimeNotifier;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<SosAlertDto> Handle(TriggerSosCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        (DailyTripInstance trip, DriverProfile driver, _, _, Vehicle vehicle) =
            await TripLifecycleLoader.LoadOwnedTripAsync(_currentUserService, _dbContext, request.TripId, cancellationToken);

        User driverUser = await _dbContext.Users
            .AsNoTracking()
            .FirstAsync(u => u.Id == driver.UserId, cancellationToken);

        int onboardCount = await _dbContext.TripStopManifests
            .AsNoTracking()
            .CountAsync(m => m.TripId == trip.Id && m.AttendanceStatus == AttendanceStatus.PickedUp, cancellationToken);

        var alert = new SosAlertDto(
            trip.Id,
            driver.Id,
            driverUser.FullName,
            vehicle.RegistrationNumber,
            request.Latitude,
            request.Longitude,
            onboardCount,
            _timeProvider.GetUtcNow(),
            request.Notes);

        await _realtimeNotifier.NotifySosTriggeredAsync(alert, cancellationToken);
        return alert;
    }
}

internal static class TripLifecycleLoader
{
    public static async Task<(DailyTripInstance Trip, DriverProfile Driver, ScheduledRoute Route, School School, Vehicle Vehicle)>
        LoadOwnedTripAsync(
            ICurrentUserService currentUserService,
            IMalumeDbContext dbContext,
            Guid tripId,
            CancellationToken cancellationToken)
    {
        User user = await CurrentUserGuard.RequireCurrentUserAsync(currentUserService, dbContext, cancellationToken);
        if (user.Role != UserRole.Driver)
        {
            throw new ForbiddenException("Only the assigned driver can operate this trip.");
        }

        DriverProfile driver = await dbContext.DriverProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.UserId == user.Id, cancellationToken)
            ?? throw new NotFoundException("Driver profile was not found.");

        DailyTripInstance trip = await dbContext.DailyTripInstances
            .FirstOrDefaultAsync(t => t.Id == tripId, cancellationToken)
            ?? throw new NotFoundException($"Trip '{tripId}' was not found.");

        if (trip.DriverProfileId != driver.Id)
        {
            throw new ForbiddenException("This trip is not assigned to the signed-in driver.");
        }

        ScheduledRoute route = await dbContext.ScheduledRoutes
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == trip.RouteId, cancellationToken)
            ?? throw new NotFoundException($"Scheduled route '{trip.RouteId}' was not found.");

        School school = await dbContext.Schools
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == route.SchoolId, cancellationToken)
            ?? throw new NotFoundException($"School '{route.SchoolId}' was not found.");

        Vehicle vehicle = await dbContext.Vehicles
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == trip.VehicleId, cancellationToken)
            ?? throw new NotFoundException($"Vehicle '{trip.VehicleId}' was not found.");

        return (trip, driver, route, school, vehicle);
    }

    public static async Task<DriverTripSummaryDto> BuildSummaryAsync(
        IMalumeDbContext dbContext,
        DailyTripInstance trip,
        ScheduledRoute route,
        School school,
        Vehicle vehicle,
        CancellationToken cancellationToken)
    {
        List<TripStopManifest> stops = await dbContext.TripStopManifests
            .AsNoTracking()
            .Where(m => m.TripId == trip.Id)
            .ToListAsync(cancellationToken);

        int completedStops = stops.Count(s => s.AttendanceStatus is AttendanceStatus.PickedUp or AttendanceStatus.DroppedOff);

        return new DriverTripSummaryDto(
            trip.Id,
            route.Id,
            route.RouteName,
            route.Direction.ToString(),
            (int)route.Direction,
            school.Id,
            school.Name,
            vehicle.Id,
            vehicle.RegistrationNumber,
            trip.ServiceDate,
            trip.Status.ToString(),
            trip.AvailableCorridorSeats,
            stops.Count,
            completedStops,
            trip.StartedAtUtc,
            trip.ArrivedAtSchoolGateUtc,
            trip.CompletedAtUtc);
    }
}

internal static class TripManifestMapper
{
    public static TripManifestDto BuildManifestDto(
        DailyTripInstance trip,
        ScheduledRoute route,
        School school,
        Vehicle vehicle,
        DriverProfile driver,
        IReadOnlyList<TripStopManifest> stops,
        IReadOnlyDictionary<Guid, Learner> learnersById,
        string qrPublicKeyBase64)
    {
        List<TripManifestStopDto> stopDtos = [];
        foreach (TripStopManifest stop in stops.OrderBy(s => s.SequenceOrder))
        {
            Learner learner = learnersById[stop.LearnerId];
            string pinVerifierHash = learner.ComputeDailyPinVerifierHash(trip.ServiceDate, route.Direction);

            stopDtos.Add(new TripManifestStopDto(
                stop.Id,
                learner.Id,
                learner.FullName,
                learner.Grade,
                learner.SchoolId,
                learner.HomeAddressText,
                learner.EmergencyContactPhone,
                stop.SequenceOrder,
                stop.StopLocation.Latitude,
                stop.StopLocation.Longitude,
                stop.BookingType.ToString(),
                stop.AttendanceStatus.ToString(),
                stop.HandoverMethod.ToString(),
                stop.HandoverTimestampUtc,
                stop.EstimatedArrivalUtc,
                qrPublicKeyBase64,
                pinVerifierHash,
                stop.WasSyncedOffline));
        }

        return new TripManifestDto(
            trip.Id,
            route.Id,
            route.RouteName,
            route.Direction.ToString(),
            (int)route.Direction,
            trip.ServiceDate,
            trip.Status.ToString(),
            school.Id,
            school.Name,
            school.SchoolGateLocation.Latitude,
            school.SchoolGateLocation.Longitude,
            school.GateGeofenceRadiusMeters,
            vehicle.RegistrationNumber,
            driver.ComplianceStatus.ToString(),
            stopDtos);
    }
}
