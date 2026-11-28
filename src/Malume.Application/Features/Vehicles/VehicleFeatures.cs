using FluentValidation;
using Malume.Application.Abstractions.Identity;
using Malume.Application.Abstractions.Persistence;
using Malume.Application.Common;
using Malume.Application.Common.Exceptions;
using Malume.Application.Common.Mappings;
using Malume.Domain.Entities;
using Malume.Domain.Enums;
using Malume.Shared.Contracts.Vehicles;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Malume.Application.Features.Vehicles;

public sealed record RegisterVehicleCommand(
    string RegistrationNumber,
    string MakeModel,
    int TotalSeatCapacity,
    DateOnly LicenseDiscExpiryDate,
    DateOnly CofExpiryDate) : IRequest<VehicleDto>;

public sealed class RegisterVehicleCommandValidator : AbstractValidator<RegisterVehicleCommand>
{
    public RegisterVehicleCommandValidator()
    {
        RuleFor(x => x.RegistrationNumber).NotEmpty().MaximumLength(32);
        RuleFor(x => x.MakeModel).NotEmpty().MaximumLength(120);
        RuleFor(x => x.TotalSeatCapacity).InclusiveBetween(1, 80);
    }
}

public sealed class RegisterVehicleCommandHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext) : IRequestHandler<RegisterVehicleCommand, VehicleDto>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;

    public async Task<VehicleDto> Handle(RegisterVehicleCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        User user = await CurrentUserGuard.RequireCurrentUserAsync(_currentUserService, _dbContext, cancellationToken);

        Guid? fleetId = null;
        Guid? schoolId = null;

        if (user.Role == UserRole.FleetOwner)
        {
            fleetId = user.TenantFleetId ?? throw new ForbiddenException("Create your fleet before registering vehicles.");
        }
        else if (user.Role == UserRole.SchoolAdmin)
        {
            schoolId = user.TenantSchoolId ?? throw new ForbiddenException("Create your school before registering vehicles.");
        }
        else
        {
            throw new ForbiddenException("Only fleet owners or school administrators can register vehicles.");
        }

        string normalizedReg = request.RegistrationNumber.Trim().ToUpperInvariant();
        bool duplicate = await _dbContext.Vehicles.AnyAsync(v => v.RegistrationNumber == normalizedReg, cancellationToken);
        if (duplicate)
        {
            throw new ConflictException($"Vehicle with registration '{normalizedReg}' already exists.");
        }

        var vehicle = new Vehicle(
            normalizedReg,
            request.MakeModel,
            request.TotalSeatCapacity,
            request.LicenseDiscExpiryDate,
            request.CofExpiryDate,
            fleetId,
            schoolId);

        await _dbContext.Vehicles.AddAsync(vehicle, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return vehicle.ToDto();
    }
}

public sealed record ListMyVehiclesQuery : IRequest<IReadOnlyList<VehicleDto>>;

public sealed class ListMyVehiclesQueryHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext) : IRequestHandler<ListMyVehiclesQuery, IReadOnlyList<VehicleDto>>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<VehicleDto>> Handle(
        ListMyVehiclesQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        User user = await CurrentUserGuard.RequireCurrentUserAsync(_currentUserService, _dbContext, cancellationToken);

        IQueryable<Vehicle> query = _dbContext.Vehicles.AsNoTracking();

        if (user.Role == UserRole.FleetOwner)
        {
            if (user.TenantFleetId is not Guid fleetId)
            {
                return [];
            }

            query = query.Where(v => v.FleetId == fleetId);
        }
        else if (user.Role == UserRole.SchoolAdmin)
        {
            if (user.TenantSchoolId is not Guid schoolId)
            {
                return [];
            }

            query = query.Where(v => v.SchoolId == schoolId);
        }
        else if (user.Role != UserRole.SysAdmin)
        {
            throw new ForbiddenException("You do not have permission to view vehicles.");
        }

        List<Vehicle> vehicles = await query
            .OrderBy(v => v.RegistrationNumber)
            .ToListAsync(cancellationToken);

        return vehicles.Select(v => v.ToDto()).ToList();
    }
}

public sealed record AssignVehicleDriverCommand(Guid VehicleId, Guid? DriverProfileId) : IRequest<VehicleDto>;

public sealed class AssignVehicleDriverCommandHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext) : IRequestHandler<AssignVehicleDriverCommand, VehicleDto>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;

    public async Task<VehicleDto> Handle(AssignVehicleDriverCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        User user = await CurrentUserGuard.RequireCurrentUserAsync(_currentUserService, _dbContext, cancellationToken);

        Vehicle? vehicle = await _dbContext.Vehicles
            .FirstOrDefaultAsync(v => v.Id == request.VehicleId, cancellationToken);

        bool belongsToCaller = vehicle is not null && user.Role switch
        {
            UserRole.FleetOwner => user.TenantFleetId is Guid fleetId && vehicle.FleetId == fleetId,
            UserRole.SchoolAdmin => user.TenantSchoolId is Guid schoolId && vehicle.SchoolId == schoolId,
            _ => false
        };

        if (!belongsToCaller || vehicle is null)
        {
            throw new NotFoundException($"Vehicle '{request.VehicleId}' was not found.");
        }

        if (request.DriverProfileId is Guid driverProfileId)
        {
            DriverProfile? driver = await _dbContext.DriverProfiles
                .FirstOrDefaultAsync(d => d.Id == driverProfileId, cancellationToken)
                ?? throw new NotFoundException($"Driver profile '{driverProfileId}' was not found.");

            if (vehicle.FleetId is Guid fleetId && driver.FleetId != fleetId)
            {
                throw new ForbiddenException("Driver does not belong to this fleet.");
            }
        }

        vehicle.AssignDriver(request.DriverProfileId);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return vehicle.ToDto();
    }
}
