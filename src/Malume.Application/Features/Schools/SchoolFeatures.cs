using FluentValidation;
using Malume.Application.Abstractions.Identity;
using Malume.Application.Abstractions.Persistence;
using Malume.Application.Common;
using Malume.Application.Common.Exceptions;
using Malume.Application.Common.Mappings;
using Malume.Domain.Entities;
using Malume.Domain.ValueObjects;
using Malume.Shared.Contracts.Tenants;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Malume.Application.Features.Schools;

public sealed record CreateSchoolCommand(
    string Name,
    string AddressText,
    double GateLatitude,
    double GateLongitude,
    string? EmisNumber,
    int GateGeofenceRadiusMeters = School.DefaultGateGeofenceRadiusMeters) : IRequest<SchoolDto>;

public sealed class CreateSchoolCommandValidator : AbstractValidator<CreateSchoolCommand>
{
    public CreateSchoolCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AddressText).NotEmpty().MaximumLength(400);
        RuleFor(x => x.EmisNumber).MaximumLength(64);
        RuleFor(x => x.GateLatitude).InclusiveBetween(-90.0, 90.0);
        RuleFor(x => x.GateLongitude).InclusiveBetween(-180.0, 180.0);
        RuleFor(x => x.GateGeofenceRadiusMeters).GreaterThan(0);
    }
}

public sealed class CreateSchoolCommandHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext) : IRequestHandler<CreateSchoolCommand, SchoolDto>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;

    public async Task<SchoolDto> Handle(CreateSchoolCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        User user = await CurrentUserGuard.RequireCurrentUserAsync(_currentUserService, _dbContext, cancellationToken);
        if (user.TenantSchoolId is not null)
        {
            throw new ConflictException("User is already assigned to a school.");
        }

        var school = new School(
            request.Name,
            new GeoCoordinate(request.GateLatitude, request.GateLongitude),
            request.AddressText,
            request.EmisNumber,
            request.GateGeofenceRadiusMeters,
            user.Id);

        await _dbContext.Schools.AddAsync(school, cancellationToken);
        user.AssignSchoolTenant(school.Id);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return school.ToDto();
    }
}

public sealed record GetMySchoolQuery : IRequest<SchoolDto>;

public sealed class GetMySchoolQueryHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext) : IRequestHandler<GetMySchoolQuery, SchoolDto>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;

    public async Task<SchoolDto> Handle(GetMySchoolQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        User user = await CurrentUserGuard.RequireCurrentUserAsync(_currentUserService, _dbContext, cancellationToken);
        if (user.TenantSchoolId is not Guid schoolId)
        {
            throw new NotFoundException("Create your school first.");
        }

        School school = await _dbContext.Schools
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == schoolId, cancellationToken)
            ?? throw new NotFoundException("School was not found.");

        return school.ToDto();
    }
}

public sealed record ListSchoolsQuery : IRequest<IReadOnlyList<SchoolDto>>;

public sealed class ListSchoolsQueryHandler(IMalumeDbContext dbContext)
    : IRequestHandler<ListSchoolsQuery, IReadOnlyList<SchoolDto>>
{
    private readonly IMalumeDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<SchoolDto>> Handle(ListSchoolsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        List<School> schools = await _dbContext.Schools
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);

        return schools.Select(s => s.ToDto()).ToList();
    }
}
