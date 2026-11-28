using FluentValidation;
using Malume.Application.Abstractions.Identity;
using Malume.Application.Abstractions.Persistence;
using Malume.Application.Common;
using Malume.Application.Common.Exceptions;
using Malume.Application.Common.Mappings;
using Malume.Domain.Entities;
using Malume.Domain.Exceptions;
using Malume.Domain.StateMachines;
using Malume.Shared.Contracts.Endorsements;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Malume.Application.Features.Endorsements;

public sealed record EndorseDriverCommand(Guid DriverProfileId, string? Notes) : IRequest<SchoolEndorsementDto>;

public sealed class EndorseDriverCommandValidator : AbstractValidator<EndorseDriverCommand>
{
    public EndorseDriverCommandValidator()
    {
        RuleFor(x => x.DriverProfileId).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public sealed class EndorseDriverCommandHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext) : IRequestHandler<EndorseDriverCommand, SchoolEndorsementDto>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;

    public async Task<SchoolEndorsementDto> Handle(EndorseDriverCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        User admin = await CurrentUserGuard.RequireCurrentUserAsync(_currentUserService, _dbContext, cancellationToken);
        if (admin.TenantSchoolId is not Guid schoolId)
        {
            throw new NotFoundException("Create your school first.");
        }

        DriverProfile driver = await _dbContext.DriverProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == request.DriverProfileId, cancellationToken)
            ?? throw new NotFoundException($"Driver profile '{request.DriverProfileId}' was not found.");

        if (!DriverComplianceStateMachine.CanStartTrip(driver.ComplianceStatus))
        {
            throw new DomainException("Driver must be NLTA compliant before receiving a school endorsement.");
        }

        User driverUser = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == driver.UserId, cancellationToken)
            ?? throw new NotFoundException("Driver user account was not found.");

        SchoolEndorsement? existing = await _dbContext.SchoolEndorsements
            .FirstOrDefaultAsync(
                e => e.SchoolId == schoolId && e.DriverProfileId == driver.Id,
                cancellationToken);

        if (existing is not null)
        {
            if (existing.IsActive)
            {
                throw new ConflictException("Driver is already endorsed by this school.");
            }

            existing.Reactivate(admin.Id, request.Notes);
        }
        else
        {
            existing = new SchoolEndorsement(schoolId, driver.Id, admin.Id, request.Notes);
            await _dbContext.SchoolEndorsements.AddAsync(existing, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return existing.ToDto(driverUser.FullName);
    }
}

public sealed record RevokeEndorsementCommand(Guid DriverProfileId) : IRequest;

public sealed class RevokeEndorsementCommandHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext) : IRequestHandler<RevokeEndorsementCommand>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;

    public async Task Handle(RevokeEndorsementCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        User admin = await CurrentUserGuard.RequireCurrentUserAsync(_currentUserService, _dbContext, cancellationToken);
        if (admin.TenantSchoolId is not Guid schoolId)
        {
            throw new NotFoundException("Create your school first.");
        }

        SchoolEndorsement endorsement = await _dbContext.SchoolEndorsements
            .FirstOrDefaultAsync(
                e => e.SchoolId == schoolId && e.DriverProfileId == request.DriverProfileId && e.IsActive,
                cancellationToken)
            ?? throw new NotFoundException("Active school endorsement was not found for this driver.");

        endorsement.Revoke();
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

public sealed record ListMySchoolEndorsementsQuery : IRequest<IReadOnlyList<SchoolEndorsementDto>>;

public sealed class ListMySchoolEndorsementsQueryHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext) : IRequestHandler<ListMySchoolEndorsementsQuery, IReadOnlyList<SchoolEndorsementDto>>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<SchoolEndorsementDto>> Handle(
        ListMySchoolEndorsementsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        User admin = await CurrentUserGuard.RequireCurrentUserAsync(_currentUserService, _dbContext, cancellationToken);
        if (admin.TenantSchoolId is not Guid schoolId)
        {
            return [];
        }

        List<SchoolEndorsementDto> endorsements = await _dbContext.SchoolEndorsements
            .AsNoTracking()
            .Where(e => e.SchoolId == schoolId)
            .Join(
                _dbContext.DriverProfiles.AsNoTracking(),
                e => e.DriverProfileId,
                d => d.Id,
                (e, d) => new { Endorsement = e, d.UserId })
            .Join(
                _dbContext.Users.AsNoTracking(),
                x => x.UserId,
                u => u.Id,
                (x, u) => x.Endorsement.ToDto(u.FullName))
            .ToListAsync(cancellationToken);

        return endorsements;
    }
}
