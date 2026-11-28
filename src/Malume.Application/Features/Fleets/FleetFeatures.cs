using FluentValidation;
using Malume.Application.Abstractions.Identity;
using Malume.Application.Abstractions.Persistence;
using Malume.Application.Common;
using Malume.Application.Common.Exceptions;
using Malume.Application.Common.Mappings;
using Malume.Domain.Entities;
using Malume.Domain.Enums;
using Malume.Shared.Contracts.Drivers;
using Malume.Shared.Contracts.Tenants;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Malume.Application.Features.Fleets;

public sealed record CreateFleetCommand(string Name, string? CompanyRegistrationNumber) : IRequest<FleetDto>;

public sealed class CreateFleetCommandValidator : AbstractValidator<CreateFleetCommand>
{
    public CreateFleetCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CompanyRegistrationNumber).MaximumLength(64);
    }
}

public sealed class CreateFleetCommandHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext) : IRequestHandler<CreateFleetCommand, FleetDto>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;

    public async Task<FleetDto> Handle(CreateFleetCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        User user = await CurrentUserGuard.RequireCurrentUserAsync(_currentUserService, _dbContext, cancellationToken);
        if (user.TenantFleetId is not null)
        {
            throw new ConflictException("User already manages a fleet.");
        }

        var fleet = new Fleet(request.Name, user.Id, request.CompanyRegistrationNumber);
        await _dbContext.Fleets.AddAsync(fleet, cancellationToken);
        user.AssignFleetTenant(fleet.Id);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return fleet.ToDto();
    }
}

public sealed record GetMyFleetQuery : IRequest<FleetDto>;

public sealed class GetMyFleetQueryHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext) : IRequestHandler<GetMyFleetQuery, FleetDto>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;

    public async Task<FleetDto> Handle(GetMyFleetQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        User user = await CurrentUserGuard.RequireCurrentUserAsync(_currentUserService, _dbContext, cancellationToken);
        if (user.TenantFleetId is not Guid fleetId)
        {
            throw new NotFoundException("Create your fleet first.");
        }

        Fleet fleet = await _dbContext.Fleets
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == fleetId, cancellationToken)
            ?? throw new NotFoundException("Fleet was not found.");

        return fleet.ToDto();
    }
}

public sealed record ListFleetDriversQuery : IRequest<IReadOnlyList<DriverProfileDto>>;

public sealed class ListFleetDriversQueryHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext) : IRequestHandler<ListFleetDriversQuery, IReadOnlyList<DriverProfileDto>>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<DriverProfileDto>> Handle(
        ListFleetDriversQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        User user = await CurrentUserGuard.RequireCurrentUserAsync(_currentUserService, _dbContext, cancellationToken);
        if (user.TenantFleetId is not Guid fleetId)
        {
            return [];
        }

        List<DriverProfileDto> drivers = await _dbContext.DriverProfiles
            .AsNoTracking()
            .Where(d => d.FleetId == fleetId)
            .Join(
                _dbContext.Users.AsNoTracking(),
                d => d.UserId,
                u => u.Id,
                (d, u) => d.ToDto(u))
            .ToListAsync(cancellationToken);

        return drivers;
    }
}

public sealed record HireDriverCommand(string DriverEmail) : IRequest<DriverProfileDto>;

public sealed class HireDriverCommandValidator : AbstractValidator<HireDriverCommand>
{
    public HireDriverCommandValidator()
    {
        RuleFor(x => x.DriverEmail).NotEmpty().EmailAddress().MaximumLength(256);
    }
}

public sealed class HireDriverCommandHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext) : IRequestHandler<HireDriverCommand, DriverProfileDto>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;

    public async Task<DriverProfileDto> Handle(HireDriverCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        User owner = await CurrentUserGuard.RequireCurrentUserAsync(_currentUserService, _dbContext, cancellationToken);
        if (owner.TenantFleetId is not Guid fleetId)
        {
            throw new NotFoundException("Create your fleet first.");
        }

        string normalizedEmail = request.DriverEmail.Trim().ToUpperInvariant();
        User? driverUser = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

        if (driverUser is null || driverUser.Role != UserRole.Driver)
        {
            throw new NotFoundException($"Driver account '{request.DriverEmail.Trim()}' was not found.");
        }

        DriverProfile? driver = await _dbContext.DriverProfiles
            .FirstOrDefaultAsync(d => d.UserId == driverUser.Id, cancellationToken);

        if (driver is null)
        {
            throw new NotFoundException("Driver profile has not been created yet.");
        }

        if (driver.FleetId is Guid existingFleetId && existingFleetId != fleetId)
        {
            throw new ConflictException("Driver is already assigned to another fleet.");
        }

        driver.AssignToFleet(fleetId);
        driverUser.AssignFleetTenant(fleetId);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return driver.ToDto(driverUser);
    }
}
