using FluentValidation;
using Malume.Application.Abstractions.Identity;
using Malume.Application.Abstractions.Persistence;
using Malume.Application.Abstractions.Security;
using Malume.Application.Common;
using Malume.Application.Common.Exceptions;
using Malume.Application.Common.Mappings;
using Malume.Domain.Common;
using Malume.Domain.Entities;
using Malume.Domain.Enums;
using Malume.Domain.ValueObjects;
using Malume.Shared.Contracts.Learners;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Malume.Application.Features.Learners;

public sealed record CreateLearnerCommand(
    Guid SchoolId,
    string FullName,
    string Grade,
    double HomeLatitude,
    double HomeLongitude,
    string HomeAddressText,
    string EmergencyContactPhone) : IRequest<LearnerDto>;

public sealed class CreateLearnerCommandValidator : AbstractValidator<CreateLearnerCommand>
{
    public CreateLearnerCommandValidator()
    {
        RuleFor(x => x.SchoolId).NotEmpty();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Grade).NotEmpty().MaximumLength(32);
        RuleFor(x => x.HomeLatitude).InclusiveBetween(-90.0, 90.0);
        RuleFor(x => x.HomeLongitude).InclusiveBetween(-180.0, 180.0);
        RuleFor(x => x.HomeAddressText).NotEmpty().MaximumLength(400);
        RuleFor(x => x.EmergencyContactPhone).NotEmpty().MaximumLength(32);
    }
}

public sealed class CreateLearnerCommandHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext) : IRequestHandler<CreateLearnerCommand, LearnerDto>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;

    public async Task<LearnerDto> Handle(CreateLearnerCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        User user = await CurrentUserGuard.RequireCurrentUserAsync(_currentUserService, _dbContext, cancellationToken);
        if (user.Role is not (UserRole.Parent or UserRole.SysAdmin))
        {
            throw new ForbiddenException("Only parents or system administrators can register learners.");
        }

        bool schoolExists = await _dbContext.Schools.AnyAsync(s => s.Id == request.SchoolId, cancellationToken);
        if (!schoolExists)
        {
            throw new NotFoundException($"School '{request.SchoolId}' was not found.");
        }

        var learner = new Learner(
            user.Id,
            request.SchoolId,
            request.FullName,
            request.Grade,
            new GeoCoordinate(request.HomeLatitude, request.HomeLongitude),
            request.HomeAddressText,
            request.EmergencyContactPhone);

        await _dbContext.Learners.AddAsync(learner, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return learner.ToDto();
    }
}

public sealed record GetLearnersQuery : IRequest<IReadOnlyList<LearnerDto>>;

public sealed class GetLearnersQueryHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext) : IRequestHandler<GetLearnersQuery, IReadOnlyList<LearnerDto>>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<LearnerDto>> Handle(GetLearnersQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        User user = await CurrentUserGuard.RequireCurrentUserAsync(_currentUserService, _dbContext, cancellationToken);
        if (user.Role is not (UserRole.Parent or UserRole.SysAdmin))
        {
            throw new ForbiddenException("Only parents or system administrators can list learners.");
        }

        IQueryable<Learner> query = _dbContext.Learners.AsNoTracking();
        if (user.Role == UserRole.Parent)
        {
            query = query.Where(l => l.ParentUserId == user.Id);
        }

        List<Learner> learners = await query.OrderBy(l => l.FullName).ToListAsync(cancellationToken);
        return learners.Select(l => l.ToDto()).ToList();
    }
}

public sealed record GetLearnerQrBadgeQuery(Guid LearnerId) : IRequest<LearnerQrBadgeDto>;

public sealed class GetLearnerQrBadgeQueryHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext,
    IQrBadgeSigner qrBadgeSigner) : IRequestHandler<GetLearnerQrBadgeQuery, LearnerQrBadgeDto>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;
    private readonly IQrBadgeSigner _qrBadgeSigner = qrBadgeSigner;

    public async Task<LearnerQrBadgeDto> Handle(GetLearnerQrBadgeQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        User user = await CurrentUserGuard.RequireCurrentUserAsync(_currentUserService, _dbContext, cancellationToken);
        Learner learner = await _dbContext.Learners
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == request.LearnerId, cancellationToken)
            ?? throw new NotFoundException($"Learner '{request.LearnerId}' was not found.");

        bool authorized = user.Role == UserRole.SysAdmin
            || (user.Role == UserRole.Parent && learner.ParentUserId == user.Id)
            || (user.Role == UserRole.SchoolAdmin && user.TenantSchoolId == learner.SchoolId);

        if (!authorized)
        {
            throw new ForbiddenException("You do not have permission to access this learner QR badge.");
        }

        string qrPayload = _qrBadgeSigner.SignLearnerBadge(learner.Id, learner.SchoolId);
        return new LearnerQrBadgeDto(
            learner.Id,
            learner.SchoolId,
            learner.FullName,
            qrPayload,
            _qrBadgeSigner.PublicKeyBase64);
    }
}

public sealed record GetLearnerDailyPinQuery(
    Guid LearnerId,
    DateOnly? ServiceDate,
    string Direction) : IRequest<DailyPinDto>;

public sealed class GetLearnerDailyPinQueryValidator : AbstractValidator<GetLearnerDailyPinQuery>
{
    public GetLearnerDailyPinQueryValidator()
    {
        RuleFor(x => x.LearnerId).NotEmpty();
        RuleFor(x => x.Direction)
            .NotEmpty()
            .Must(d => Enum.TryParse<RouteDirection>(d, ignoreCase: true, out _))
            .WithMessage("Direction must be MorningToSchool or AfternoonFromSchool.");
    }
}

public sealed class GetLearnerDailyPinQueryHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext,
    TimeProvider timeProvider) : IRequestHandler<GetLearnerDailyPinQuery, DailyPinDto>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<DailyPinDto> Handle(GetLearnerDailyPinQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        User user = await CurrentUserGuard.RequireCurrentUserAsync(_currentUserService, _dbContext, cancellationToken);
        Learner learner = await _dbContext.Learners
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == request.LearnerId, cancellationToken)
            ?? throw new NotFoundException($"Learner '{request.LearnerId}' was not found.");

        bool authorized = user.Role == UserRole.SysAdmin
            || (user.Role == UserRole.Parent && learner.ParentUserId == user.Id);

        if (!authorized)
        {
            throw new ForbiddenException("Only the learner's parent or a system administrator can view the daily PIN.");
        }

        RouteDirection direction = Enum.Parse<RouteDirection>(request.Direction, ignoreCase: true);
        DateOnly serviceDate = request.ServiceDate ?? SastClock.TodaySast(_timeProvider.GetUtcNow());
        string pin = learner.DeriveDailyPin(serviceDate, direction);
        string verifierHash = learner.ComputeDailyPinVerifierHash(serviceDate, direction);

        return new DailyPinDto(
            learner.Id,
            serviceDate,
            direction.ToString(),
            (int)direction,
            pin,
            verifierHash);
    }
}
