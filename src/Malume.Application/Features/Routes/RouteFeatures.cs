using System.Globalization;
using FluentValidation;
using Malume.Application.Abstractions.Identity;
using Malume.Application.Abstractions.Persistence;
using Malume.Application.Common;
using Malume.Application.Common.Exceptions;
using Malume.Application.Common.Mappings;
using Malume.Domain.Common;
using Malume.Domain.Entities;
using Malume.Domain.Enums;
using Malume.Domain.ValueObjects;
using Malume.Shared.Contracts.Routes;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Malume.Application.Features.Routes;

public sealed record CreateScheduledRouteCommand(
    Guid VehicleId,
    Guid DriverProfileId,
    Guid SchoolId,
    string RouteName,
    string Direction,
    IReadOnlyList<GeoPointDto> CorridorWaypoints,
    decimal MonthlyPriceZar) : IRequest<ScheduledRouteDto>;

public sealed class CreateScheduledRouteCommandValidator : AbstractValidator<CreateScheduledRouteCommand>
{
    public CreateScheduledRouteCommandValidator()
    {
        RuleFor(x => x.VehicleId).NotEmpty();
        RuleFor(x => x.DriverProfileId).NotEmpty();
        RuleFor(x => x.SchoolId).NotEmpty();
        RuleFor(x => x.RouteName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Direction)
            .NotEmpty()
            .Must(d => Enum.TryParse<RouteDirection>(d, ignoreCase: true, out _))
            .WithMessage("Direction must be MorningToSchool or AfternoonFromSchool.");
        RuleFor(x => x.CorridorWaypoints)
            .NotNull()
            .Must(w => w.Count >= 2)
            .WithMessage("At least 2 corridor waypoints are required.");
        RuleForEach(x => x.CorridorWaypoints).ChildRules(point =>
        {
            point.RuleFor(p => p.Latitude).InclusiveBetween(-90.0, 90.0);
            point.RuleFor(p => p.Longitude).InclusiveBetween(-180.0, 180.0);
        });
        RuleFor(x => x.MonthlyPriceZar).GreaterThan(0m);
    }
}

public sealed class CreateScheduledRouteCommandHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext) : IRequestHandler<CreateScheduledRouteCommand, ScheduledRouteDto>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;

    public async Task<ScheduledRouteDto> Handle(
        CreateScheduledRouteCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        User user = await CurrentUserGuard.RequireCurrentUserAsync(_currentUserService, _dbContext, cancellationToken);

        Vehicle vehicle = await _dbContext.Vehicles
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == request.VehicleId, cancellationToken)
            ?? throw new NotFoundException($"Vehicle '{request.VehicleId}' was not found.");

        DriverProfile driver = await _dbContext.DriverProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == request.DriverProfileId, cancellationToken)
            ?? throw new NotFoundException($"Driver profile '{request.DriverProfileId}' was not found.");

        bool schoolExists = await _dbContext.Schools.AnyAsync(s => s.Id == request.SchoolId, cancellationToken);
        if (!schoolExists)
        {
            throw new NotFoundException($"School '{request.SchoolId}' was not found.");
        }

        bool authorized = user.Role switch
        {
            UserRole.SysAdmin => true,
            UserRole.FleetOwner => vehicle.FleetId.HasValue && user.TenantFleetId == vehicle.FleetId.Value,
            UserRole.SchoolAdmin => (vehicle.SchoolId.HasValue && user.TenantSchoolId == vehicle.SchoolId.Value)
                || user.TenantSchoolId == request.SchoolId,
            UserRole.Driver => driver.UserId == user.Id,
            _ => false
        };

        if (!authorized)
        {
            throw new ForbiddenException("You are not authorized to create a route for this vehicle or school.");
        }

        RouteDirection direction = Enum.Parse<RouteDirection>(request.Direction, ignoreCase: true);
        List<GeoCoordinate> waypoints = request.CorridorWaypoints
            .Select(p => new GeoCoordinate(p.Latitude, p.Longitude))
            .ToList();

        var route = new ScheduledRoute(
            vehicle.Id,
            driver.Id,
            request.SchoolId,
            request.RouteName,
            direction,
            new GeoCorridor(waypoints),
            new ZarMoney(request.MonthlyPriceZar));

        await _dbContext.ScheduledRoutes.AddAsync(route, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return route.ToDto();
    }
}

public sealed record GetScheduledRoutesQuery : IRequest<IReadOnlyList<ScheduledRouteDto>>;

public sealed class GetScheduledRoutesQueryHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext) : IRequestHandler<GetScheduledRoutesQuery, IReadOnlyList<ScheduledRouteDto>>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<ScheduledRouteDto>> Handle(
        GetScheduledRoutesQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        User user = await CurrentUserGuard.RequireCurrentUserAsync(_currentUserService, _dbContext, cancellationToken);
        IQueryable<ScheduledRoute> query = _dbContext.ScheduledRoutes.AsNoTracking();

        switch (user.Role)
        {
            case UserRole.SysAdmin:
            case UserRole.Parent:
                break;
            case UserRole.Driver:
                Guid? driverProfileId = await _dbContext.DriverProfiles
                    .AsNoTracking()
                    .Where(d => d.UserId == user.Id)
                    .Select(d => (Guid?)d.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (driverProfileId is null)
                {
                    return [];
                }
                query = query.Where(r => r.DriverProfileId == driverProfileId.Value);
                break;
            case UserRole.FleetOwner:
                if (user.TenantFleetId is not Guid fleetId)
                {
                    return [];
                }
                List<Guid> fleetVehicleIds = await _dbContext.Vehicles
                    .AsNoTracking()
                    .Where(v => v.FleetId == fleetId)
                    .Select(v => v.Id)
                    .ToListAsync(cancellationToken);
                query = query.Where(r => fleetVehicleIds.Contains(r.VehicleId));
                break;
            case UserRole.SchoolAdmin:
                if (user.TenantSchoolId is not Guid schoolId)
                {
                    return [];
                }
                query = query.Where(r => r.SchoolId == schoolId);
                break;
            default:
                throw new ForbiddenException("Role is not permitted to query scheduled routes.");
        }

        List<ScheduledRoute> routes = await query.OrderBy(r => r.RouteName).ToListAsync(cancellationToken);
        return routes.Select(r => r.ToDto()).ToList();
    }
}

public sealed record EnrollLearnerOnRouteCommand(
    Guid RouteId,
    Guid LearnerId,
    DateOnly? BillingMonthStart) : IRequest<RouteEnrollmentDto>;

public sealed class EnrollLearnerOnRouteCommandValidator : AbstractValidator<EnrollLearnerOnRouteCommand>
{
    public EnrollLearnerOnRouteCommandValidator()
    {
        RuleFor(x => x.RouteId).NotEmpty();
        RuleFor(x => x.LearnerId).NotEmpty();
    }
}

public sealed class EnrollLearnerOnRouteCommandHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext,
    TimeProvider timeProvider) : IRequestHandler<EnrollLearnerOnRouteCommand, RouteEnrollmentDto>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<RouteEnrollmentDto> Handle(
        EnrollLearnerOnRouteCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        User user = await CurrentUserGuard.RequireCurrentUserAsync(_currentUserService, _dbContext, cancellationToken);

        ScheduledRoute route = await _dbContext.ScheduledRoutes
            .FirstOrDefaultAsync(r => r.Id == request.RouteId, cancellationToken)
            ?? throw new NotFoundException($"Scheduled route '{request.RouteId}' was not found.");

        Vehicle vehicle = await _dbContext.Vehicles
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == route.VehicleId, cancellationToken)
            ?? throw new NotFoundException($"Vehicle '{route.VehicleId}' was not found.");

        Learner learner = await _dbContext.Learners
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == request.LearnerId, cancellationToken)
            ?? throw new NotFoundException($"Learner '{request.LearnerId}' was not found.");

        bool authorized = user.Role switch
        {
            UserRole.SysAdmin => true,
            UserRole.Parent => learner.ParentUserId == user.Id,
            UserRole.FleetOwner => vehicle.FleetId.HasValue && user.TenantFleetId == vehicle.FleetId.Value,
            UserRole.SchoolAdmin => user.TenantSchoolId == route.SchoolId,
            _ => false
        };

        if (!authorized)
        {
            throw new ForbiddenException("You are not authorized to enroll this learner on the route.");
        }

        DateOnly today = SastClock.TodaySast(_timeProvider.GetUtcNow());
        DateOnly requestedDate = request.BillingMonthStart ?? today;
        var monthStart = new DateOnly(requestedDate.Year, requestedDate.Month, 1);
        string billingYearMonth = monthStart.ToString("yyyy-MM", CultureInfo.InvariantCulture);

        bool alreadyEnrolled = await _dbContext.Subscriptions.AnyAsync(
            s => s.LearnerId == learner.Id
                 && s.RouteId == route.Id
                 && s.BillingYearMonth == billingYearMonth
                 && s.EscrowStatus == EscrowStatus.Active,
            cancellationToken);

        if (alreadyEnrolled)
        {
            throw new ConflictException($"Learner '{learner.FullName}' is already subscribed to this route for {billingYearMonth}.");
        }

        route.ReserveSeat(vehicle.TotalSeatCapacity);
        var subscription = Subscription.CreateWithStagedEscrow(
            learner.ParentUserId,
            learner.Id,
            route.Id,
            billingYearMonth,
            route.MonthlyPrice,
            monthStart);

        await _dbContext.Subscriptions.AddAsync(subscription, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return subscription.ToDto();
    }
}
