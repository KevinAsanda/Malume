using FluentValidation;
using Malume.Application.Abstractions.Identity;
using Malume.Application.Abstractions.Persistence;
using Malume.Application.Abstractions.Realtime;
using Malume.Application.Common;
using Malume.Application.Common.Exceptions;
using Malume.Application.Features.Trips;
using Malume.Domain.Common;
using Malume.Domain.Entities;
using Malume.Domain.Enums;
using Malume.Domain.Exceptions;
using Malume.Domain.ValueObjects;
using Malume.Shared.Contracts.Corridor;
using Malume.Shared.Contracts.Trips;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Malume.Application.Features.Corridor;

public sealed record SearchCorridorOptionsQuery(
    Guid SchoolId,
    DateOnly? ServiceDate,
    double PickupLatitude,
    double PickupLongitude,
    double MaxDistanceMeters,
    string? Direction) : IRequest<IReadOnlyList<CorridorAdHocOptionDto>>;

public sealed class SearchCorridorOptionsQueryValidator : AbstractValidator<SearchCorridorOptionsQuery>
{
    public SearchCorridorOptionsQueryValidator()
    {
        RuleFor(x => x.SchoolId).NotEmpty();
        RuleFor(x => x.PickupLatitude).InclusiveBetween(-90.0, 90.0);
        RuleFor(x => x.PickupLongitude).InclusiveBetween(-180.0, 180.0);
        RuleFor(x => x.MaxDistanceMeters).GreaterThan(0.0).LessThanOrEqualTo(GeoMath.PeakCorridorRadiusMeters);
    }
}

public sealed class SearchCorridorOptionsQueryHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext,
    ISpatialCorridorQueryService spatialQueryService,
    TimeProvider timeProvider) : IRequestHandler<SearchCorridorOptionsQuery, IReadOnlyList<CorridorAdHocOptionDto>>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;
    private readonly ISpatialCorridorQueryService _spatialQueryService = spatialQueryService;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<IReadOnlyList<CorridorAdHocOptionDto>> Handle(
        SearchCorridorOptionsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        User user = await CurrentUserGuard.RequireCurrentUserAsync(_currentUserService, _dbContext, cancellationToken);
        if (user.Role is not (UserRole.Parent or UserRole.SysAdmin or UserRole.SchoolAdmin))
        {
            throw new ForbiddenException("Only parents, school administrators, or system administrators can search corridor seats.");
        }

        DateOnly serviceDate = request.ServiceDate ?? SastClock.TodaySast(_timeProvider.GetUtcNow());
        double radius = Math.Min(request.MaxDistanceMeters, GeoMath.PeakCorridorRadiusMeters);

        return await _spatialQueryService.FindCorridorOptionsAsync(
            request.SchoolId,
            serviceDate,
            request.PickupLatitude,
            request.PickupLongitude,
            radius,
            request.Direction,
            cancellationToken);
    }
}

public sealed record BookCorridorSeatCommand(
    Guid TripId,
    Guid LearnerId,
    double? PickupLatitude,
    double? PickupLongitude) : IRequest<CorridorBookingResultDto>;

public sealed class BookCorridorSeatCommandValidator : AbstractValidator<BookCorridorSeatCommand>
{
    public BookCorridorSeatCommandValidator()
    {
        RuleFor(x => x.TripId).NotEmpty();
        RuleFor(x => x.LearnerId).NotEmpty();
        When(x => x.PickupLatitude.HasValue, () =>
            RuleFor(x => x.PickupLatitude!.Value).InclusiveBetween(-90.0, 90.0));
        When(x => x.PickupLongitude.HasValue, () =>
            RuleFor(x => x.PickupLongitude!.Value).InclusiveBetween(-180.0, 180.0));
    }
}

public sealed class BookCorridorSeatCommandHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext,
    ISpatialCorridorQueryService spatialQueryService,
    ITripRealtimeNotifier realtimeNotifier,
    TimeProvider timeProvider) : IRequestHandler<BookCorridorSeatCommand, CorridorBookingResultDto>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;
    private readonly ISpatialCorridorQueryService _spatialQueryService = spatialQueryService;
    private readonly ITripRealtimeNotifier _realtimeNotifier = realtimeNotifier;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<CorridorBookingResultDto> Handle(
        BookCorridorSeatCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        User user = await CurrentUserGuard.RequireCurrentUserAsync(_currentUserService, _dbContext, cancellationToken);
        if (user.Role is not (UserRole.Parent or UserRole.SysAdmin))
        {
            throw new ForbiddenException("Only parents or system administrators can book corridor seats.");
        }

        DailyTripInstance trip = await _dbContext.DailyTripInstances
            .FirstOrDefaultAsync(t => t.Id == request.TripId, cancellationToken)
            ?? throw new NotFoundException($"Trip '{request.TripId}' was not found.");

        ScheduledRoute route = await _dbContext.ScheduledRoutes
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == trip.RouteId, cancellationToken)
            ?? throw new NotFoundException($"Scheduled route '{trip.RouteId}' was not found.");

        Learner learner = await _dbContext.Learners
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == request.LearnerId, cancellationToken)
            ?? throw new NotFoundException($"Learner '{request.LearnerId}' was not found.");

        if (user.Role == UserRole.Parent && learner.ParentUserId != user.Id)
        {
            throw new ForbiddenException("Parents may only book corridor seats for their own registered learners.");
        }

        if (learner.SchoolId != route.SchoolId)
        {
            throw new DomainException("This route serves a different school than the learner's enrolled school.");
        }

        if (trip.Status is not (TripLifecycleStatus.Scheduled or TripLifecycleStatus.EnRoute))
        {
            throw new DomainException($"Cannot book corridor seat when trip status is '{trip.Status}'.");
        }

        DriverProfile driver = await _dbContext.DriverProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == trip.DriverProfileId, cancellationToken)
            ?? throw new NotFoundException("Assigned driver profile was not found.");

        if (driver.ComplianceStatus is not (DriverComplianceStatus.ActiveCompliant or DriverComplianceStatus.ActiveExpiringSoon))
        {
            throw new DomainException("Cannot book a seat on a suspended or unapproved driver trip.");
        }

        GeoCoordinate pickupCoordinate = request.PickupLatitude.HasValue && request.PickupLongitude.HasValue
            ? new GeoCoordinate(request.PickupLatitude.Value, request.PickupLongitude.Value)
            : learner.HomePickupPoint;

        double? distanceMeters = await _spatialQueryService.GetDistanceToRouteCorridorMetersAsync(
            route.Id,
            pickupCoordinate.Latitude,
            pickupCoordinate.Longitude,
            cancellationToken);

        if (distanceMeters is null || distanceMeters.Value > GeoMath.PeakCorridorRadiusMeters)
        {
            throw new DomainException(
                $"Pickup location lies outside the 1.5 km spatial corridor ({distanceMeters?.ToString("F0", System.Globalization.CultureInfo.InvariantCulture) ?? "unknown"} m away).");
        }

        List<TripStopManifest> existingStops = await _dbContext.TripStopManifests
            .Where(m => m.TripId == trip.Id)
            .ToListAsync(cancellationToken);

        if (existingStops.Any(s => s.LearnerId == learner.Id && s.AttendanceStatus != AttendanceStatus.MarkedAbsent))
        {
            throw new ConflictException($"Learner '{learner.FullName}' is already manifested on this trip.");
        }

        trip.DecrementAvailableCorridorSeat();

        int insertionOrder = RouteOptimizationService.ComputeInsertionSequenceOrder(existingStops, pickupCoordinate);
        var newStop = new TripStopManifest(
            trip.Id,
            learner.Id,
            BookingType.AdHocCorridor,
            insertionOrder,
            pickupCoordinate);

        await _dbContext.TripStopManifests.AddAsync(newStop, cancellationToken);
        existingStops.Add(newStop);

        List<Guid> allLearnerIds = existingStops.Select(s => s.LearnerId).Distinct().ToList();
        Dictionary<Guid, Learner> learnersById = await _dbContext.Learners
            .AsNoTracking()
            .Where(l => allLearnerIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, cancellationToken);

        GeoCoordinate origin = driver.CurrentLocation ?? route.CorridorPath.Points[0];
        DateTimeOffset departureAnchor = trip.StartedAtUtc ?? _timeProvider.GetUtcNow();
        IReadOnlyList<EtaCascadeUpdateDto> etaUpdates = RouteOptimizationService.RecomputeDownstreamEtas(
            origin,
            departureAnchor,
            existingStops,
            learnersById);

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _realtimeNotifier.NotifyManifestMutatedAsync(
            trip.Id,
            learner.Id,
            "AdHocCorridorBooked",
            trip.AvailableCorridorSeats,
            cancellationToken);

        if (etaUpdates.Count > 0)
        {
            await _realtimeNotifier.NotifyEtaCascadeAsync(trip.Id, etaUpdates, cancellationToken);
        }

        return new CorridorBookingResultDto(
            trip.Id,
            newStop.Id,
            learner.Id,
            newStop.SequenceOrder,
            trip.AvailableCorridorSeats,
            newStop.EstimatedArrivalUtc);
    }
}

public sealed record SearchOffPeakDriversQuery(
    double PickupLatitude,
    double PickupLongitude,
    double MaxDistanceMeters = GeoMath.OffPeakSearchRadiusMeters) : IRequest<IReadOnlyList<OffPeakDriverOptionDto>>;

public sealed class SearchOffPeakDriversQueryValidator : AbstractValidator<SearchOffPeakDriversQuery>
{
    public SearchOffPeakDriversQueryValidator()
    {
        RuleFor(x => x.PickupLatitude).InclusiveBetween(-90.0, 90.0);
        RuleFor(x => x.PickupLongitude).InclusiveBetween(-180.0, 180.0);
        RuleFor(x => x.MaxDistanceMeters).GreaterThan(0.0).LessThanOrEqualTo(25_000.0);
    }
}

public sealed class SearchOffPeakDriversQueryHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext,
    ISpatialCorridorQueryService spatialQueryService) : IRequestHandler<SearchOffPeakDriversQuery, IReadOnlyList<OffPeakDriverOptionDto>>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;
    private readonly ISpatialCorridorQueryService _spatialQueryService = spatialQueryService;

    public async Task<IReadOnlyList<OffPeakDriverOptionDto>> Handle(
        SearchOffPeakDriversQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        User user = await CurrentUserGuard.RequireCurrentUserAsync(_currentUserService, _dbContext, cancellationToken);
        if (user.Role is not (UserRole.Parent or UserRole.SysAdmin))
        {
            throw new ForbiddenException("Only parents or system administrators can search off-peak drivers.");
        }

        return await _spatialQueryService.FindNearbyOffPeakDriversAsync(
            request.PickupLatitude,
            request.PickupLongitude,
            request.MaxDistanceMeters,
            cancellationToken);
    }
}
