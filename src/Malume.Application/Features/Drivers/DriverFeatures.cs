using FluentValidation;
using Malume.Application.Abstractions.Identity;
using Malume.Application.Abstractions.Persistence;
using Malume.Application.Common;
using Malume.Application.Common.Exceptions;
using Malume.Application.Common.Mappings;
using Malume.Domain.Entities;
using Malume.Domain.Enums;
using Malume.Domain.Exceptions;
using Malume.Shared.Contracts.Drivers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Malume.Application.Features.Drivers;

public sealed record CreateMyDriverProfileCommand(
    string PrdpNumber,
    DateOnly? PrdpExpiryDate,
    DateOnly? PoliceClearanceExpiryDate) : IRequest<DriverProfileDto>;

public sealed class CreateMyDriverProfileCommandValidator : AbstractValidator<CreateMyDriverProfileCommand>
{
    public CreateMyDriverProfileCommandValidator()
    {
        RuleFor(x => x.PrdpNumber).NotEmpty().MaximumLength(64);
    }
}

public sealed class CreateMyDriverProfileCommandHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext) : IRequestHandler<CreateMyDriverProfileCommand, DriverProfileDto>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;

    public async Task<DriverProfileDto> Handle(
        CreateMyDriverProfileCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        User user = await CurrentUserGuard.RequireCurrentUserAsync(_currentUserService, _dbContext, cancellationToken);
        bool exists = await _dbContext.DriverProfiles.AnyAsync(d => d.UserId == user.Id, cancellationToken);
        if (exists)
        {
            throw new ConflictException("Driver profile already exists for this user.");
        }

        var profile = new DriverProfile(
            user.Id,
            request.PrdpNumber,
            request.PrdpExpiryDate,
            request.PoliceClearanceExpiryDate,
            user.TenantFleetId);

        await _dbContext.DriverProfiles.AddAsync(profile, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return profile.ToDto(user);
    }
}

public sealed record GetMyDriverProfileQuery : IRequest<DriverProfileDto>;

public sealed class GetMyDriverProfileQueryHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext) : IRequestHandler<GetMyDriverProfileQuery, DriverProfileDto>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;

    public async Task<DriverProfileDto> Handle(GetMyDriverProfileQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        User user = await CurrentUserGuard.RequireCurrentUserAsync(_currentUserService, _dbContext, cancellationToken);
        DriverProfile profile = await _dbContext.DriverProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.UserId == user.Id, cancellationToken)
            ?? throw new NotFoundException("Driver profile was not found.");

        return profile.ToDto(user);
    }
}

public sealed record ListDriversQuery(string? Status) : IRequest<IReadOnlyList<DriverProfileDto>>;

public sealed class ListDriversQueryHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext) : IRequestHandler<ListDriversQuery, IReadOnlyList<DriverProfileDto>>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<DriverProfileDto>> Handle(
        ListDriversQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        User user = await CurrentUserGuard.RequireCurrentUserAsync(_currentUserService, _dbContext, cancellationToken);

        IQueryable<DriverProfile> query = _dbContext.DriverProfiles.AsNoTracking();

        if (user.Role == UserRole.FleetOwner)
        {
            if (user.TenantFleetId is not Guid fleetId)
            {
                return [];
            }

            query = query.Where(d => d.FleetId == fleetId);
        }
        else if (user.Role is not (UserRole.SysAdmin or UserRole.SchoolAdmin))
        {
            throw new ForbiddenException("You do not have permission to list drivers.");
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<DriverComplianceStatus>(request.Status, ignoreCase: true, out DriverComplianceStatus parsedStatus))
            {
                throw new DomainException($"Invalid driver compliance status '{request.Status}'.");
            }

            query = query.Where(d => d.ComplianceStatus == parsedStatus);
        }

        List<DriverProfileDto> drivers = await query
            .Join(
                _dbContext.Users.AsNoTracking(),
                d => d.UserId,
                u => u.Id,
                (d, u) => d.ToDto(u))
            .ToListAsync(cancellationToken);

        return drivers;
    }
}
