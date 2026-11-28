using FluentValidation;
using Malume.Application.Abstractions.Identity;
using Malume.Application.Abstractions.Persistence;
using Malume.Application.Common;
using Malume.Application.Common.Exceptions;
using Malume.Domain.Entities;
using Malume.Domain.Enums;
using Malume.Shared.Contracts.Trips;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Malume.Application.Features.Sos;

public sealed record GetSosIncidentsQuery(bool ActiveOnly = false) : IRequest<IReadOnlyList<SosAlertDto>>;

public sealed class GetSosIncidentsQueryHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext) : IRequestHandler<GetSosIncidentsQuery, IReadOnlyList<SosAlertDto>>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<SosAlertDto>> Handle(
        GetSosIncidentsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        User user = await CurrentUserGuard.RequireCurrentUserAsync(_currentUserService, _dbContext, cancellationToken);
        if (user.Role is not (UserRole.SysAdmin or UserRole.SchoolAdmin or UserRole.FleetOwner))
        {
            throw new ForbiddenException("Only administrators or fleet owners can query SOS incidents.");
        }

        IQueryable<SosIncident> query = _dbContext.SosIncidents.AsNoTracking();

        if (request.ActiveOnly)
        {
            query = query.Where(s => s.Status != SosIncidentStatus.Resolved);
        }

        if (user.Role == UserRole.SchoolAdmin)
        {
            if (!user.TenantSchoolId.HasValue)
            {
                return [];
            }

            Guid schoolId = user.TenantSchoolId.Value;
            query = query.Where(s => s.SchoolId == schoolId);
        }
        else if (user.Role == UserRole.FleetOwner)
        {
            if (!user.TenantFleetId.HasValue)
            {
                return [];
            }

            Guid fleetId = user.TenantFleetId.Value;
            query = query.Where(s => _dbContext.DailyTripInstances.Any(
                t => t.Id == s.TripId
                     && _dbContext.Vehicles.Any(v => v.Id == t.VehicleId && v.FleetId == fleetId)));
        }

        List<SosIncident> incidents = await query
            .OrderByDescending(s => s.TriggeredAtUtc)
            .ToListAsync(cancellationToken);

        return incidents.Select(SosIncidentMapper.ToDto).ToList();
    }
}

public sealed record AcknowledgeSosIncidentCommand(Guid IncidentId) : IRequest<SosAlertDto>;

public sealed class AcknowledgeSosIncidentCommandHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext,
    TimeProvider timeProvider) : IRequestHandler<AcknowledgeSosIncidentCommand, SosAlertDto>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<SosAlertDto> Handle(
        AcknowledgeSosIncidentCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        User user = await CurrentUserGuard.RequireCurrentUserAsync(_currentUserService, _dbContext, cancellationToken);
        SosIncident incident = await _dbContext.SosIncidents
            .FirstOrDefaultAsync(s => s.Id == request.IncidentId, cancellationToken)
            ?? throw new NotFoundException($"SOS incident '{request.IncidentId}' was not found.");

        EnsureCanManageIncident(user, incident);

        incident.Acknowledge(_timeProvider.GetUtcNow());
        await _dbContext.SaveChangesAsync(cancellationToken);

        return SosIncidentMapper.ToDto(incident);
    }

    internal static void EnsureCanManageIncident(User user, SosIncident incident)
    {
        bool authorized = user.Role == UserRole.SysAdmin
            || (user.Role == UserRole.SchoolAdmin && user.TenantSchoolId == incident.SchoolId);

        if (!authorized)
        {
            throw new ForbiddenException("Only system administrators or administrators of this school can manage this SOS incident.");
        }
    }
}

public sealed record ResolveSosIncidentCommand(
    Guid IncidentId,
    string? ResolutionNotes) : IRequest<SosAlertDto>;

public sealed class ResolveSosIncidentCommandValidator : AbstractValidator<ResolveSosIncidentCommand>
{
    public ResolveSosIncidentCommandValidator()
    {
        RuleFor(x => x.IncidentId).NotEmpty();
        RuleFor(x => x.ResolutionNotes).MaximumLength(500);
    }
}

public sealed class ResolveSosIncidentCommandHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext,
    TimeProvider timeProvider) : IRequestHandler<ResolveSosIncidentCommand, SosAlertDto>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<SosAlertDto> Handle(
        ResolveSosIncidentCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        User user = await CurrentUserGuard.RequireCurrentUserAsync(_currentUserService, _dbContext, cancellationToken);
        SosIncident incident = await _dbContext.SosIncidents
            .FirstOrDefaultAsync(s => s.Id == request.IncidentId, cancellationToken)
            ?? throw new NotFoundException($"SOS incident '{request.IncidentId}' was not found.");

        AcknowledgeSosIncidentCommandHandler.EnsureCanManageIncident(user, incident);

        incident.Resolve(user.Id, request.ResolutionNotes, _timeProvider.GetUtcNow());
        await _dbContext.SaveChangesAsync(cancellationToken);

        return SosIncidentMapper.ToDto(incident);
    }
}

internal static class SosIncidentMapper
{
    public static SosAlertDto ToDto(SosIncident s) =>
        new(
            s.TripId,
            s.DriverProfileId,
            s.DriverFullName,
            s.VehicleRegistration,
            s.Location.Latitude,
            s.Location.Longitude,
            s.OnboardLearnerCount,
            s.TriggeredAtUtc,
            s.Notes,
            s.Id,
            s.SchoolId,
            s.TriggeredByRole.ToString(),
            s.Status.ToString(),
            s.AcknowledgedAtUtc,
            s.ResolvedAtUtc,
            s.ResolutionNotes);
}
