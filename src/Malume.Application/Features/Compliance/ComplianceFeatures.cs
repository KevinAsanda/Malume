using FluentValidation;
using Malume.Application.Abstractions.Identity;
using Malume.Application.Abstractions.Jobs;
using Malume.Application.Abstractions.Persistence;
using Malume.Application.Common;
using Malume.Application.Common.Exceptions;
using Malume.Application.Common.Mappings;
using Malume.Domain.Common;
using Malume.Domain.Entities;
using Malume.Domain.Enums;
using Malume.Domain.Exceptions;
using Malume.Shared.Contracts.Compliance;
using Malume.Shared.Contracts.Drivers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Malume.Application.Features.Compliance;

public sealed record SubmitComplianceDocumentCommand(
    string DocumentType,
    string DocumentNumber,
    string StorageUrl,
    DateOnly ExpiryDate,
    Guid? VehicleId) : IRequest<ComplianceDocumentDto>;

public sealed class SubmitComplianceDocumentCommandValidator : AbstractValidator<SubmitComplianceDocumentCommand>
{
    public SubmitComplianceDocumentCommandValidator()
    {
        RuleFor(x => x.DocumentType)
            .NotEmpty()
            .Must(BeValidDocumentType)
            .WithMessage("DocumentType must be one of: Prdp, PoliceClearance, LicenseDisc, CertificateOfFitness.");
        RuleFor(x => x.DocumentNumber).NotEmpty().MaximumLength(100);
        RuleFor(x => x.StorageUrl).NotEmpty().MaximumLength(1024);
    }

    private static bool BeValidDocumentType(string documentType) =>
        Enum.TryParse<ComplianceDocumentType>(documentType, ignoreCase: true, out _);
}

public sealed class SubmitComplianceDocumentCommandHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext) : IRequestHandler<SubmitComplianceDocumentCommand, ComplianceDocumentDto>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;

    public async Task<ComplianceDocumentDto> Handle(
        SubmitComplianceDocumentCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        User user = await CurrentUserGuard.RequireCurrentUserAsync(_currentUserService, _dbContext, cancellationToken);
        ComplianceDocumentType documentType = Enum.Parse<ComplianceDocumentType>(request.DocumentType, ignoreCase: true);

        Guid? driverProfileId = null;
        Guid? vehicleId = null;

        if (documentType is ComplianceDocumentType.Prdp or ComplianceDocumentType.PoliceClearance)
        {
            if (user.Role != UserRole.Driver)
            {
                throw new ForbiddenException("Only drivers can submit PrDP or police clearance documents.");
            }

            if (request.VehicleId is not null)
            {
                throw new DomainException("Driver compliance documents must not specify a VehicleId.");
            }

            DriverProfile driver = await _dbContext.DriverProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.UserId == user.Id, cancellationToken)
                ?? throw new DomainException("Create your driver profile before submitting compliance documents.");

            driverProfileId = driver.Id;
        }
        else
        {
            if (user.Role is not (UserRole.FleetOwner or UserRole.SchoolAdmin))
            {
                throw new ForbiddenException("Only fleet owners or school administrators can submit vehicle documents.");
            }

            if (request.VehicleId is not Guid targetVehicleId)
            {
                throw new DomainException("VehicleId is required for vehicle compliance documents.");
            }

            Vehicle? vehicle = await _dbContext.Vehicles
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == targetVehicleId, cancellationToken)
                ?? throw new NotFoundException($"Vehicle '{targetVehicleId}' was not found.");

            bool belongsToCaller = user.Role switch
            {
                UserRole.FleetOwner => user.TenantFleetId is Guid fleetId && vehicle.FleetId == fleetId,
                UserRole.SchoolAdmin => user.TenantSchoolId is Guid schoolId && vehicle.SchoolId == schoolId,
                _ => false
            };

            if (!belongsToCaller)
            {
                throw new ForbiddenException("Vehicle does not belong to your fleet or school.");
            }

            vehicleId = vehicle.Id;
        }

        var document = new ComplianceDocument(
            documentType,
            request.DocumentNumber,
            request.StorageUrl,
            request.ExpiryDate,
            driverProfileId,
            vehicleId);

        await _dbContext.ComplianceDocuments.AddAsync(document, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return document.ToDto();
    }
}

public sealed record ListMyComplianceDocumentsQuery : IRequest<IReadOnlyList<ComplianceDocumentDto>>;

public sealed class ListMyComplianceDocumentsQueryHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext) : IRequestHandler<ListMyComplianceDocumentsQuery, IReadOnlyList<ComplianceDocumentDto>>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<ComplianceDocumentDto>> Handle(
        ListMyComplianceDocumentsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        User user = await CurrentUserGuard.RequireCurrentUserAsync(_currentUserService, _dbContext, cancellationToken);

        if (user.Role == UserRole.Driver)
        {
            Guid? driverProfileId = await _dbContext.DriverProfiles
                .AsNoTracking()
                .Where(d => d.UserId == user.Id)
                .Select(d => (Guid?)d.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (driverProfileId is not Guid id)
            {
                return [];
            }

            List<ComplianceDocument> driverDocs = await _dbContext.ComplianceDocuments
                .AsNoTracking()
                .Where(c => c.DriverProfileId == id)
                .OrderByDescending(c => c.CreatedAtUtc)
                .ToListAsync(cancellationToken);

            return driverDocs.Select(d => d.ToDto()).ToList();
        }

        List<Guid> vehicleIds;
        if (user.Role == UserRole.FleetOwner && user.TenantFleetId is Guid fleetId)
        {
            vehicleIds = await _dbContext.Vehicles
                .AsNoTracking()
                .Where(v => v.FleetId == fleetId)
                .Select(v => v.Id)
                .ToListAsync(cancellationToken);
        }
        else if (user.Role == UserRole.SchoolAdmin && user.TenantSchoolId is Guid schoolId)
        {
            vehicleIds = await _dbContext.Vehicles
                .AsNoTracking()
                .Where(v => v.SchoolId == schoolId)
                .Select(v => v.Id)
                .ToListAsync(cancellationToken);
        }
        else
        {
            return [];
        }

        if (vehicleIds.Count == 0)
        {
            return [];
        }

        List<ComplianceDocument> vehicleDocs = await _dbContext.ComplianceDocuments
            .AsNoTracking()
            .Where(c => c.VehicleId != null && vehicleIds.Contains(c.VehicleId.Value))
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return vehicleDocs.Select(d => d.ToDto()).ToList();
    }
}

public sealed record GetComplianceReviewQueueQuery : IRequest<ComplianceReviewQueueDto>;

public sealed class GetComplianceReviewQueueQueryHandler(IMalumeDbContext dbContext)
    : IRequestHandler<GetComplianceReviewQueueQuery, ComplianceReviewQueueDto>
{
    private readonly IMalumeDbContext _dbContext = dbContext;

    public async Task<ComplianceReviewQueueDto> Handle(
        GetComplianceReviewQueueQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        List<Guid> driversWithUnverifiedDocs = await _dbContext.ComplianceDocuments
            .AsNoTracking()
            .Where(c => c.DriverProfileId != null && !c.IsVerified)
            .Select(c => c.DriverProfileId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        List<DriverProfileDto> drivers = await _dbContext.DriverProfiles
            .AsNoTracking()
            .Where(d => d.ComplianceStatus == DriverComplianceStatus.PendingReview
                || d.ComplianceStatus == DriverComplianceStatus.ActiveExpiringSoon
                || d.ComplianceStatus == DriverComplianceStatus.SuspendedExpiredDoc
                || driversWithUnverifiedDocs.Contains(d.Id))
            .Join(
                _dbContext.Users.AsNoTracking(),
                d => d.UserId,
                u => u.Id,
                (d, u) => d.ToDto(u))
            .ToListAsync(cancellationToken);

        List<Guid> queuedDriverIds = drivers.Select(d => d.Id).ToList();

        List<ComplianceDocument> driverDocuments = queuedDriverIds.Count == 0
            ? []
            : await _dbContext.ComplianceDocuments
                .AsNoTracking()
                .Where(c => c.DriverProfileId != null && queuedDriverIds.Contains(c.DriverProfileId.Value))
                .OrderByDescending(c => c.CreatedAtUtc)
                .ToListAsync(cancellationToken);

        Dictionary<Guid, List<ComplianceDocumentDto>> docsByDriver = driverDocuments
            .GroupBy(c => c.DriverProfileId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(c => c.ToDto()).ToList());

        List<ComplianceReviewItemDto> reviewItems = drivers
            .Select(d => new ComplianceReviewItemDto(
                d,
                docsByDriver.TryGetValue(d.Id, out List<ComplianceDocumentDto>? docs) ? docs : []))
            .ToList();

        List<ComplianceDocument> unverifiedVehicleDocs = await _dbContext.ComplianceDocuments
            .AsNoTracking()
            .Where(c => c.VehicleId != null && !c.IsVerified)
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return new ComplianceReviewQueueDto(
            reviewItems,
            unverifiedVehicleDocs.Select(c => c.ToDto()).ToList());
    }
}

public sealed record VerifyComplianceDocumentCommand(Guid DocumentId) : IRequest<ComplianceDocumentDto>;

public sealed class VerifyComplianceDocumentCommandHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext) : IRequestHandler<VerifyComplianceDocumentCommand, ComplianceDocumentDto>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;

    public async Task<ComplianceDocumentDto> Handle(
        VerifyComplianceDocumentCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        User admin = await CurrentUserGuard.RequireCurrentUserAsync(_currentUserService, _dbContext, cancellationToken);

        ComplianceDocument document = await _dbContext.ComplianceDocuments
            .FirstOrDefaultAsync(c => c.Id == request.DocumentId, cancellationToken)
            ?? throw new NotFoundException($"Compliance document '{request.DocumentId}' was not found.");

        document.Verify(admin.Id);

        if (document.DriverProfileId is Guid driverId)
        {
            DriverProfile driver = await _dbContext.DriverProfiles
                .FirstOrDefaultAsync(d => d.Id == driverId, cancellationToken)
                ?? throw new NotFoundException($"Driver profile '{driverId}' was not found.");

            driver.UpdateDocumentExpiry(document.DocumentType, document.ExpiryDate);
        }
        else if (document.VehicleId is Guid vehicleId)
        {
            Vehicle vehicle = await _dbContext.Vehicles
                .FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken)
                ?? throw new NotFoundException($"Vehicle '{vehicleId}' was not found.");

            vehicle.UpdateDocumentExpiry(document.DocumentType, document.ExpiryDate);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return document.ToDto();
    }
}

public sealed record ApproveDriverComplianceCommand(Guid DriverProfileId) : IRequest<DriverProfileDto>;

public sealed class ApproveDriverComplianceCommandHandler(
    IMalumeDbContext dbContext,
    TimeProvider timeProvider) : IRequestHandler<ApproveDriverComplianceCommand, DriverProfileDto>
{
    private readonly IMalumeDbContext _dbContext = dbContext;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<DriverProfileDto> Handle(
        ApproveDriverComplianceCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        DriverProfile driver = await _dbContext.DriverProfiles
            .FirstOrDefaultAsync(d => d.Id == request.DriverProfileId, cancellationToken)
            ?? throw new NotFoundException($"Driver profile '{request.DriverProfileId}' was not found.");

        User driverUser = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == driver.UserId, cancellationToken)
            ?? throw new NotFoundException("Driver user account was not found.");

        List<ComplianceDocument> verifiedDocs = await _dbContext.ComplianceDocuments
            .AsNoTracking()
            .Where(c => c.DriverProfileId == driver.Id && c.IsVerified)
            .OrderByDescending(c => c.VerifiedAtUtc ?? c.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        DateOnly todaySast = SastClock.TodaySast(_timeProvider.GetUtcNow());

        ComplianceDocument? prdp = verifiedDocs.FirstOrDefault(c => c.DocumentType == ComplianceDocumentType.Prdp);
        ComplianceDocument? police = verifiedDocs.FirstOrDefault(c => c.DocumentType == ComplianceDocumentType.PoliceClearance);

        if (prdp is null || police is null || prdp.ExpiryDate <= todaySast || police.ExpiryDate <= todaySast)
        {
            throw new DomainException("Driver requires verified, unexpired PrDP and police clearance documents.");
        }

        driver.ApproveCompliance(prdp.ExpiryDate, police.ExpiryDate);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return driver.ToDto(driverUser);
    }
}

public sealed record SuspendDriverCommand(Guid DriverProfileId) : IRequest<DriverProfileDto>;

public sealed class SuspendDriverCommandHandler(
    IMalumeDbContext dbContext,
    EscrowFreezeService escrowFreezeService) : IRequestHandler<SuspendDriverCommand, DriverProfileDto>
{
    private readonly IMalumeDbContext _dbContext = dbContext;
    private readonly EscrowFreezeService _escrowFreezeService = escrowFreezeService;

    public async Task<DriverProfileDto> Handle(SuspendDriverCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        DriverProfile driver = await _dbContext.DriverProfiles
            .FirstOrDefaultAsync(d => d.Id == request.DriverProfileId, cancellationToken)
            ?? throw new NotFoundException($"Driver profile '{request.DriverProfileId}' was not found.");

        User driverUser = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == driver.UserId, cancellationToken)
            ?? throw new NotFoundException("Driver user account was not found.");

        driver.SuspendForSafetyIncident();
        await _escrowFreezeService.PauseActiveSubscriptionsForDriverAsync(driver.Id, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return driver.ToDto(driverUser);
    }
}

public sealed record RunComplianceExpiryScanCommand : IRequest<ComplianceScanResultDto>;

public sealed class RunComplianceExpiryScanCommandHandler(IComplianceExpiryScanner scanner)
    : IRequestHandler<RunComplianceExpiryScanCommand, ComplianceScanResultDto>
{
    private readonly IComplianceExpiryScanner _scanner = scanner;

    public Task<ComplianceScanResultDto> Handle(
        RunComplianceExpiryScanCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _scanner.ScanAsync(cancellationToken);
    }
}
