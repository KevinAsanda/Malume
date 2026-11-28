using System.Globalization;
using System.Text.Json;
using FluentValidation;
using Malume.Application.Abstractions.Identity;
using Malume.Application.Abstractions.Jobs;
using Malume.Application.Abstractions.Payments;
using Malume.Application.Abstractions.Persistence;
using Malume.Application.Common;
using Malume.Application.Common.Exceptions;
using Malume.Application.Common.Mappings;
using Malume.Domain.Common;
using Malume.Domain.Entities;
using Malume.Domain.Enums;
using Malume.Domain.Exceptions;
using Malume.Domain.Services;
using Malume.Domain.ValueObjects;
using Malume.Shared.Contracts.Escrow;
using Malume.Shared.Contracts.Tenants;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Malume.Application.Features.Escrow;

public sealed record InitializeSubscriptionPaymentCommand(
    Guid RouteId,
    Guid LearnerId,
    DateOnly? BillingMonthStart,
    string? CallbackUrl) : IRequest<InitializeSubscriptionPaymentResponse>;

public sealed class InitializeSubscriptionPaymentCommandValidator : AbstractValidator<InitializeSubscriptionPaymentCommand>
{
    public InitializeSubscriptionPaymentCommandValidator()
    {
        RuleFor(x => x.RouteId).NotEmpty();
        RuleFor(x => x.LearnerId).NotEmpty();
        When(x => !string.IsNullOrWhiteSpace(x.CallbackUrl), () =>
            RuleFor(x => x.CallbackUrl!).MaximumLength(512));
    }
}

public sealed class InitializeSubscriptionPaymentCommandHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext,
    IPaystackGateway paystackGateway,
    TimeProvider timeProvider) : IRequestHandler<InitializeSubscriptionPaymentCommand, InitializeSubscriptionPaymentResponse>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;
    private readonly IPaystackGateway _paystackGateway = paystackGateway;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<InitializeSubscriptionPaymentResponse> Handle(
        InitializeSubscriptionPaymentCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        User user = await CurrentUserGuard.RequireCurrentUserAsync(_currentUserService, _dbContext, cancellationToken);
        if (user.Role is not (UserRole.Parent or UserRole.SysAdmin))
        {
            throw new ForbiddenException("Only parents or system administrators can initialize subscription payments.");
        }

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

        if (user.Role == UserRole.Parent && learner.ParentUserId != user.Id)
        {
            throw new ForbiddenException("Parents may only pay for their own learners.");
        }

        DateOnly today = SastClock.TodaySast(_timeProvider.GetUtcNow());
        DateOnly requestedDate = request.BillingMonthStart ?? today;
        var monthStart = new DateOnly(requestedDate.Year, requestedDate.Month, 1);
        string billingYearMonth = monthStart.ToString("yyyy-MM", CultureInfo.InvariantCulture);

        Subscription? subscription = await _dbContext.Subscriptions
            .Include(s => s.Milestones)
            .FirstOrDefaultAsync(
                s => s.LearnerId == learner.Id
                     && s.RouteId == route.Id
                     && s.BillingYearMonth == billingYearMonth,
                cancellationToken);

        bool isNewSubscription = subscription is null;
        if (subscription is null)
        {
            route.ReserveSeat(vehicle.TotalSeatCapacity);
            subscription = Subscription.CreateWithStagedEscrow(
                learner.ParentUserId,
                learner.Id,
                route.Id,
                billingYearMonth,
                route.MonthlyPrice,
                monthStart);
            await _dbContext.Subscriptions.AddAsync(subscription, cancellationToken);
        }

        string? subaccountCode = null;
        if (vehicle.FleetId is Guid fleetId)
        {
            subaccountCode = await _dbContext.Fleets
                .AsNoTracking()
                .Where(f => f.Id == fleetId)
                .Select(f => f.PaystackSubaccountCode)
                .FirstOrDefaultAsync(cancellationToken);
        }

        string reference = !string.IsNullOrWhiteSpace(subscription.PaystackReference)
            ? subscription.PaystackReference
            : $"MLM-{subscription.Id:N}"[..24];

        if (isNewSubscription || string.IsNullOrWhiteSpace(subscription.PaystackReference))
        {
            subscription.SetPaystackReference(reference);
        }

        PaystackInitializeResult initResult = await _paystackGateway.InitializeTransactionAsync(
            user.Email,
            subscription.TotalAmount,
            reference,
            subaccountCode,
            request.CallbackUrl,
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new InitializeSubscriptionPaymentResponse(
            subscription.Id,
            initResult.Reference,
            initResult.AuthorizationUrl,
            initResult.AccessCode,
            _paystackGateway.PublicKey,
            subscription.TotalAmount.Amount);
    }
}

public sealed record VerifySubscriptionPaymentCommand(string Reference) : IRequest<VerifySubscriptionPaymentResponse>;

public sealed class VerifySubscriptionPaymentCommandHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext,
    IPaystackGateway paystackGateway) : IRequestHandler<VerifySubscriptionPaymentCommand, VerifySubscriptionPaymentResponse>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;
    private readonly IPaystackGateway _paystackGateway = paystackGateway;

    public async Task<VerifySubscriptionPaymentResponse> Handle(
        VerifySubscriptionPaymentCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        User user = await CurrentUserGuard.RequireCurrentUserAsync(_currentUserService, _dbContext, cancellationToken);

        Subscription subscription = await _dbContext.Subscriptions
            .FirstOrDefaultAsync(s => s.PaystackReference == request.Reference, cancellationToken)
            ?? throw new NotFoundException($"Subscription for payment reference '{request.Reference}' was not found.");

        if (user.Role == UserRole.Parent && subscription.ParentUserId != user.Id)
        {
            throw new ForbiddenException("You do not have permission to verify this subscription payment.");
        }

        PaystackVerifyResult verifyResult = await _paystackGateway.VerifyTransactionAsync(request.Reference, cancellationToken);
        if (verifyResult.Verified)
        {
            await SubscriptionChargeRecorder.EnsureChargeRecordedAsync(_dbContext, subscription, cancellationToken);
        }

        return new VerifySubscriptionPaymentResponse(
            subscription.Id,
            request.Reference,
            subscription.EscrowStatus.ToString(),
            verifyResult.Verified,
            subscription.TotalAmount.Amount);
    }
}

public sealed record ProcessPaystackWebhookCommand(
    string RawPayload,
    string SignatureHeader) : IRequest<bool>;

public sealed class ProcessPaystackWebhookCommandHandler(
    IMalumeDbContext dbContext,
    IPaystackGateway paystackGateway) : IRequestHandler<ProcessPaystackWebhookCommand, bool>
{
    private readonly IMalumeDbContext _dbContext = dbContext;
    private readonly IPaystackGateway _paystackGateway = paystackGateway;

    public async Task<bool> Handle(ProcessPaystackWebhookCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_paystackGateway.VerifyWebhookSignature(request.RawPayload, request.SignatureHeader))
        {
            throw new UnauthorizedException("Invalid Paystack webhook signature.");
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(request.RawPayload);
            string? eventName = doc.RootElement.TryGetProperty("event", out JsonElement eventEl)
                ? eventEl.GetString()
                : null;

            if (string.Equals(eventName, "charge.success", StringComparison.OrdinalIgnoreCase)
                && doc.RootElement.TryGetProperty("data", out JsonElement dataEl)
                && dataEl.TryGetProperty("reference", out JsonElement refEl))
            {
                string? reference = refEl.GetString();
                if (!string.IsNullOrWhiteSpace(reference))
                {
                    Subscription? subscription = await _dbContext.Subscriptions
                        .FirstOrDefaultAsync(s => s.PaystackReference == reference, cancellationToken);
                    if (subscription is not null)
                    {
                        await SubscriptionChargeRecorder.EnsureChargeRecordedAsync(_dbContext, subscription, cancellationToken);
                    }
                }
            }
        }
        catch (JsonException ex)
        {
            throw new DomainException($"Malformed Paystack webhook JSON payload: {ex.Message}");
        }

        return true;
    }
}

internal static class SubscriptionChargeRecorder
{
    public static async Task EnsureChargeRecordedAsync(
        IMalumeDbContext dbContext,
        Subscription subscription,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(subscription.PaystackReference))
        {
            return;
        }

        bool alreadyRecorded = await dbContext.WalletLedgerEntries.AnyAsync(
            w => w.ReferenceType == LedgerReferenceType.SubscriptionCharge
                 && w.PaystackReference == subscription.PaystackReference,
            cancellationToken);

        if (alreadyRecorded)
        {
            return;
        }

        IReadOnlyList<WalletLedgerEntry> entries = EscrowAccountingService.CreateSubscriptionChargeEntries(
            subscription.TotalAmount,
            subscription.PaystackReference,
            $"Subscription {subscription.BillingYearMonth} charge settled into escrow");

        await dbContext.WalletLedgerEntries.AddRangeAsync(entries, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public sealed record ConfigureFleetSubaccountCommand(
    string BusinessName,
    string SettlementBankCode,
    string AccountNumber,
    decimal PercentageCharge) : IRequest<FleetDto>;

public sealed class ConfigureFleetSubaccountCommandValidator : AbstractValidator<ConfigureFleetSubaccountCommand>
{
    public ConfigureFleetSubaccountCommandValidator()
    {
        RuleFor(x => x.BusinessName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.SettlementBankCode).NotEmpty().MaximumLength(32);
        RuleFor(x => x.AccountNumber).NotEmpty().MaximumLength(32);
        RuleFor(x => x.PercentageCharge).InclusiveBetween(0m, 50m);
    }
}

public sealed class ConfigureFleetSubaccountCommandHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext,
    IPaystackGateway paystackGateway) : IRequestHandler<ConfigureFleetSubaccountCommand, FleetDto>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;
    private readonly IPaystackGateway _paystackGateway = paystackGateway;

    public async Task<FleetDto> Handle(ConfigureFleetSubaccountCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        User user = await CurrentUserGuard.RequireCurrentUserAsync(_currentUserService, _dbContext, cancellationToken);
        if (user.Role != UserRole.FleetOwner)
        {
            throw new ForbiddenException("Only fleet owners can configure Paystack payout subaccounts.");
        }

        Fleet fleet = await _dbContext.Fleets
            .FirstOrDefaultAsync(f => f.OwnerUserId == user.Id, cancellationToken)
            ?? throw new NotFoundException("Fleet profile was not found for the current user.");

        PaystackSubaccountResult created = await _paystackGateway.CreateSubaccountAsync(
            request.BusinessName,
            request.SettlementBankCode,
            request.AccountNumber,
            request.PercentageCharge,
            cancellationToken);

        fleet.SetPaystackSubaccount(created.SubaccountCode);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return fleet.ToDto();
    }
}

public sealed record GetEscrowSubscriptionsQuery : IRequest<IReadOnlyList<SubscriptionEscrowDto>>;

public sealed class GetEscrowSubscriptionsQueryHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext) : IRequestHandler<GetEscrowSubscriptionsQuery, IReadOnlyList<SubscriptionEscrowDto>>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<SubscriptionEscrowDto>> Handle(
        GetEscrowSubscriptionsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        User user = await CurrentUserGuard.RequireCurrentUserAsync(_currentUserService, _dbContext, cancellationToken);

        IQueryable<Subscription> query = _dbContext.Subscriptions
            .Include(s => s.Milestones)
            .AsNoTracking();

        switch (user.Role)
        {
            case UserRole.SysAdmin:
                break;
            case UserRole.Parent:
                query = query.Where(s => s.ParentUserId == user.Id);
                break;
            case UserRole.Driver:
                Guid? driverId = await _dbContext.DriverProfiles
                    .AsNoTracking()
                    .Where(d => d.UserId == user.Id)
                    .Select(d => (Guid?)d.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (driverId is null)
                {
                    return [];
                }
                List<Guid> driverRouteIds = await _dbContext.ScheduledRoutes
                    .AsNoTracking()
                    .Where(r => r.DriverProfileId == driverId.Value)
                    .Select(r => r.Id)
                    .ToListAsync(cancellationToken);
                query = query.Where(s => driverRouteIds.Contains(s.RouteId));
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
                List<Guid> fleetRouteIds = await _dbContext.ScheduledRoutes
                    .AsNoTracking()
                    .Where(r => fleetVehicleIds.Contains(r.VehicleId))
                    .Select(r => r.Id)
                    .ToListAsync(cancellationToken);
                query = query.Where(s => fleetRouteIds.Contains(s.RouteId));
                break;
            default:
                throw new ForbiddenException("Your role does not have access to escrow subscriptions.");
        }

        List<Subscription> subscriptions = await query
            .OrderByDescending(s => s.BillingYearMonth)
            .ToListAsync(cancellationToken);

        if (subscriptions.Count == 0)
        {
            return [];
        }

        List<Guid> routeIds = subscriptions.Select(s => s.RouteId).Distinct().ToList();
        Dictionary<Guid, ScheduledRoute> routesById = await _dbContext.ScheduledRoutes
            .AsNoTracking()
            .Where(r => routeIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, cancellationToken);

        List<Guid> learnerIds = subscriptions.Select(s => s.LearnerId).Distinct().ToList();
        Dictionary<Guid, Learner> learnersById = await _dbContext.Learners
            .AsNoTracking()
            .Where(l => learnerIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, cancellationToken);

        return subscriptions.Select(sub =>
        {
            ScheduledRoute route = routesById[sub.RouteId];
            Learner learner = learnersById[sub.LearnerId];
            List<EscrowMilestoneDto> milestones = sub.Milestones
                .OrderBy(m => m.TrancheNumber)
                .Select(m => new EscrowMilestoneDto(
                    m.Id,
                    m.TrancheNumber,
                    m.Percentage,
                    m.Amount.Amount,
                    m.Status.ToString(),
                    m.ScheduledReleaseDate,
                    m.ReleasedAtUtc,
                    m.PaystackTransferCode))
                .ToList();

            return new SubscriptionEscrowDto(
                sub.Id,
                sub.RouteId,
                route.RouteName,
                sub.LearnerId,
                learner.FullName,
                sub.BillingYearMonth,
                sub.TotalAmount.Amount,
                sub.EscrowStatus.ToString(),
                sub.PaystackReference,
                milestones);
        }).ToList();
    }
}

public sealed record GetWalletLedgerQuery(Guid? WalletAccountId) : IRequest<WalletLedgerSummaryDto>;

public sealed class GetWalletLedgerQueryHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext) : IRequestHandler<GetWalletLedgerQuery, WalletLedgerSummaryDto>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;

    public async Task<WalletLedgerSummaryDto> Handle(GetWalletLedgerQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        User user = await CurrentUserGuard.RequireCurrentUserAsync(_currentUserService, _dbContext, cancellationToken);

        Guid targetWalletId = user.Role == UserRole.SysAdmin
            ? request.WalletAccountId ?? EscrowAccountingService.PlatformEscrowLiabilityAccountId
            : user.Id;

        if (user.Role != UserRole.SysAdmin && request.WalletAccountId.HasValue && request.WalletAccountId.Value != user.Id)
        {
            throw new ForbiddenException("You may only view your own wallet ledger.");
        }

        List<WalletLedgerEntry> entries = await _dbContext.WalletLedgerEntries
            .AsNoTracking()
            .Where(w => w.WalletAccountId == targetWalletId)
            .OrderByDescending(w => w.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        decimal totalCredits = entries
            .Where(e => e.EntryType == LedgerEntryType.Credit)
            .Sum(e => e.Amount.Amount);
        decimal totalDebits = entries
            .Where(e => e.EntryType == LedgerEntryType.Debit)
            .Sum(e => e.Amount.Amount);

        List<WalletLedgerEntryDto> entryDtos = entries
            .Select(e => new WalletLedgerEntryDto(
                e.Id,
                e.TransactionGroupId,
                e.WalletAccountId,
                e.EntryType.ToString(),
                e.Amount.Amount,
                e.ReferenceType.ToString(),
                e.PaystackReference,
                e.Notes,
                e.CreatedAtUtc))
            .ToList();

        return new WalletLedgerSummaryDto(
            targetWalletId,
            totalCredits,
            totalDebits,
            totalCredits - totalDebits,
            entryDtos);
    }
}

public sealed record ProcessProRataRefundCommand(
    Guid SubscriptionId,
    string Reason) : IRequest<ProRataRefundResponse>;

public sealed class ProcessProRataRefundCommandValidator : AbstractValidator<ProcessProRataRefundCommand>
{
    public ProcessProRataRefundCommandValidator()
    {
        RuleFor(x => x.SubscriptionId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(400);
    }
}

public sealed class ProcessProRataRefundCommandHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext,
    IPaystackGateway paystackGateway) : IRequestHandler<ProcessProRataRefundCommand, ProRataRefundResponse>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;
    private readonly IPaystackGateway _paystackGateway = paystackGateway;

    public async Task<ProRataRefundResponse> Handle(
        ProcessProRataRefundCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        User user = await CurrentUserGuard.RequireCurrentUserAsync(_currentUserService, _dbContext, cancellationToken);
        if (user.Role != UserRole.SysAdmin)
        {
            throw new ForbiddenException("Only system administrators can issue pro-rata escrow refunds.");
        }

        Subscription subscription = await _dbContext.Subscriptions
            .Include(s => s.Milestones)
            .FirstOrDefaultAsync(s => s.Id == request.SubscriptionId, cancellationToken)
            ?? throw new NotFoundException($"Subscription '{request.SubscriptionId}' was not found.");

        List<EscrowMilestone> refundableMilestones = subscription.Milestones
            .Where(m => m.Status is MilestoneStatus.Frozen or MilestoneStatus.Pending)
            .ToList();

        if (refundableMilestones.Count == 0)
        {
            throw new DomainException("No unreleased escrow milestones remain to refund on this subscription.");
        }

        decimal refundSum = refundableMilestones.Sum(m => m.Amount.Amount);
        var refundAmount = new ZarMoney(refundSum);
        string txRef = subscription.PaystackReference ?? $"MLM-TEST-{subscription.Id:N}"[..20];

        PaystackRefundResult refundResult = await _paystackGateway.InitiateRefundAsync(
            txRef,
            refundAmount,
            request.Reason,
            cancellationToken);

        foreach (EscrowMilestone milestone in refundableMilestones)
        {
            if (milestone.Status == MilestoneStatus.Pending)
            {
                milestone.Freeze();
            }
            milestone.MarkRefunded();
        }

        IReadOnlyList<WalletLedgerEntry> ledgerPair = EscrowAccountingService.CreateProRataRefundEntries(
            subscription.ParentUserId,
            refundAmount,
            refundResult.RefundReference,
            $"Pro-rata refund: {request.Reason}");

        await _dbContext.WalletLedgerEntries.AddRangeAsync(ledgerPair, cancellationToken);
        subscription.MarkCompletedIfAllSettled();
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ProRataRefundResponse(
            subscription.Id,
            refundAmount.Amount,
            refundableMilestones.Count,
            refundResult.RefundReference);
    }
}

public sealed record RunEscrowPayoutsCommand(DateOnly? AsOfDateSast) : IRequest<EscrowPayoutRunResultDto>;

public sealed class RunEscrowPayoutsCommandHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext,
    IEscrowPayoutJob escrowPayoutJob) : IRequestHandler<RunEscrowPayoutsCommand, EscrowPayoutRunResultDto>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;
    private readonly IEscrowPayoutJob _escrowPayoutJob = escrowPayoutJob;

    public async Task<EscrowPayoutRunResultDto> Handle(
        RunEscrowPayoutsCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        User user = await CurrentUserGuard.RequireCurrentUserAsync(_currentUserService, _dbContext, cancellationToken);
        if (user.Role != UserRole.SysAdmin)
        {
            throw new ForbiddenException("Only system administrators can manually trigger staged escrow payouts.");
        }

        return await _escrowPayoutJob.ProcessDueMilestonesAsync(request.AsOfDateSast, cancellationToken);
    }
}
