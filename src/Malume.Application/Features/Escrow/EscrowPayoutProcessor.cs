using System.Globalization;
using Malume.Application.Abstractions.Jobs;
using Malume.Application.Abstractions.Payments;
using Malume.Application.Abstractions.Persistence;
using Malume.Domain.Common;
using Malume.Domain.Entities;
using Malume.Domain.Enums;
using Malume.Domain.Services;
using Malume.Shared.Contracts.Escrow;
using Microsoft.EntityFrameworkCore;

namespace Malume.Application.Features.Escrow;

public sealed class EscrowPayoutProcessor(
    IMalumeDbContext dbContext,
    IPaystackGateway paystackGateway,
    TimeProvider timeProvider) : IEscrowPayoutJob
{
    private const double MidMonthRequiredCompletionRatio = 0.85;

    private readonly IMalumeDbContext _dbContext = dbContext;
    private readonly IPaystackGateway _paystackGateway = paystackGateway;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<EscrowPayoutRunResultDto> ProcessDueMilestonesAsync(
        DateOnly? asOfDateSast,
        CancellationToken cancellationToken)
    {
        DateOnly evaluatedDate = asOfDateSast ?? SastClock.TodaySast(_timeProvider.GetUtcNow());

        List<Subscription> activeSubscriptions = await _dbContext.Subscriptions
            .Include(s => s.Milestones)
            .Where(s => s.EscrowStatus == EscrowStatus.Active
                        && s.Milestones.Any(m => m.Status == MilestoneStatus.Pending && m.ScheduledReleaseDate <= evaluatedDate))
            .ToListAsync(cancellationToken);

        if (activeSubscriptions.Count == 0)
        {
            return new EscrowPayoutRunResultDto(evaluatedDate, 0, 0, 0, 0, 0m);
        }

        List<Guid> routeIds = activeSubscriptions.Select(s => s.RouteId).Distinct().ToList();
        Dictionary<Guid, ScheduledRoute> routesById = await _dbContext.ScheduledRoutes
            .AsNoTracking()
            .Where(r => routeIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, cancellationToken);

        List<Guid> driverIds = routesById.Values.Select(r => r.DriverProfileId).Distinct().ToList();
        Dictionary<Guid, DriverProfile> driversById = await _dbContext.DriverProfiles
            .AsNoTracking()
            .Where(d => driverIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, cancellationToken);

        List<Guid> vehicleIds = routesById.Values.Select(r => r.VehicleId).Distinct().ToList();
        Dictionary<Guid, Vehicle> vehiclesById = await _dbContext.Vehicles
            .AsNoTracking()
            .Where(v => vehicleIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, cancellationToken);

        List<Guid> fleetIds = vehiclesById.Values
            .Where(v => v.FleetId.HasValue)
            .Select(v => v.FleetId!.Value)
            .Distinct()
            .ToList();
        Dictionary<Guid, Fleet> fleetsById = fleetIds.Count == 0
            ? []
            : await _dbContext.Fleets
                .AsNoTracking()
                .Where(f => fleetIds.Contains(f.Id))
                .ToDictionaryAsync(f => f.Id, cancellationToken);

        int releasedCount = 0;
        int pausedCount = 0;
        int skippedBelowRatioCount = 0;
        decimal totalReleasedZar = 0m;

        foreach (Subscription subscription in activeSubscriptions)
        {
            ScheduledRoute route = routesById[subscription.RouteId];
            DriverProfile driver = driversById[route.DriverProfileId];
            Vehicle vehicle = vehiclesById[route.VehicleId];

            bool isDriverCompliant = driver.ComplianceStatus is DriverComplianceStatus.ActiveCompliant or DriverComplianceStatus.ActiveExpiringSoon;
            if (!isDriverCompliant)
            {
                subscription.PauseForComplianceOrServiceFailure();
                pausedCount++;
                continue;
            }

            (Guid beneficiaryWalletId, string recipientCode) = ResolveBeneficiary(driver, vehicle, fleetsById);

            DateOnly monthStart = ParseBillingMonthStart(subscription.BillingYearMonth);
            DateOnly midMonthEnd = monthStart.AddDays(14);
            DateOnly monthEnd = new(monthStart.Year, monthStart.Month, DateTime.DaysInMonth(monthStart.Year, monthStart.Month));

            List<EscrowMilestone> dueMilestones = subscription.Milestones
                .Where(m => m.Status == MilestoneStatus.Pending && m.ScheduledReleaseDate <= evaluatedDate)
                .OrderBy(m => m.TrancheNumber)
                .ToList();

            foreach (EscrowMilestone milestone in dueMilestones)
            {
                bool eligible = milestone.TrancheNumber switch
                {
                    1 => true,
                    2 => await HasMinimumTripCompletionRatioAsync(route.Id, monthStart, midMonthEnd, cancellationToken),
                    3 => await HasVerifiedMonthEndHandoverAsync(route.Id, monthStart.AddDays(15), monthEnd, cancellationToken),
                    _ => false
                };

                if (!eligible)
                {
                    if (milestone.TrancheNumber == 2)
                    {
                        skippedBelowRatioCount++;
                    }
                    continue;
                }

                string payoutReference = $"MLM-T{milestone.TrancheNumber}-{milestone.Id:N}"[..24];
                PaystackTransferResult payout = await _paystackGateway.InitiateMilestonePayoutAsync(
                    recipientCode,
                    milestone.Amount,
                    $"Malume Tranche {milestone.TrancheNumber} ({subscription.BillingYearMonth})",
                    payoutReference,
                    cancellationToken);

                milestone.Release(payout.TransferCode);
                IReadOnlyList<WalletLedgerEntry> ledgerPair = EscrowAccountingService.CreateMilestoneReleaseEntries(
                    beneficiaryWalletId,
                    milestone.TrancheNumber,
                    milestone.Amount,
                    payout.TransferCode,
                    $"Route {route.RouteName} • {subscription.BillingYearMonth} • Tranche {milestone.TrancheNumber}");

                await _dbContext.WalletLedgerEntries.AddRangeAsync(ledgerPair, cancellationToken);
                releasedCount++;
                totalReleasedZar += milestone.Amount.Amount;
            }

            subscription.MarkCompletedIfAllSettled();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new EscrowPayoutRunResultDto(
            evaluatedDate,
            activeSubscriptions.Count,
            releasedCount,
            pausedCount,
            skippedBelowRatioCount,
            totalReleasedZar);
    }

    private async Task<bool> HasMinimumTripCompletionRatioAsync(
        Guid routeId,
        DateOnly windowStart,
        DateOnly windowEnd,
        CancellationToken cancellationToken)
    {
        List<DailyTripInstance> windowTrips = await _dbContext.DailyTripInstances
            .AsNoTracking()
            .Where(t => t.RouteId == routeId && t.ServiceDate >= windowStart && t.ServiceDate <= windowEnd)
            .ToListAsync(cancellationToken);

        if (windowTrips.Count == 0)
        {
            return false;
        }

        int completedCount = windowTrips.Count(t => t.Status == TripLifecycleStatus.Completed);
        double ratio = (double)completedCount / windowTrips.Count;
        return ratio >= MidMonthRequiredCompletionRatio;
    }

    private async Task<bool> HasVerifiedMonthEndHandoverAsync(
        Guid routeId,
        DateOnly windowStart,
        DateOnly windowEnd,
        CancellationToken cancellationToken)
    {
        List<Guid> tripIds = await _dbContext.DailyTripInstances
            .AsNoTracking()
            .Where(t => t.RouteId == routeId
                        && t.ServiceDate >= windowStart
                        && t.ServiceDate <= windowEnd
                        && t.Status == TripLifecycleStatus.Completed)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        if (tripIds.Count == 0)
        {
            return false;
        }

        return await _dbContext.TripStopManifests
            .AsNoTracking()
            .AnyAsync(
                m => tripIds.Contains(m.TripId)
                     && m.AttendanceStatus == AttendanceStatus.DroppedOff
                     && m.HandoverMethod != HandoverMethod.None,
                cancellationToken);
    }

    private static (Guid WalletId, string RecipientCode) ResolveBeneficiary(
        DriverProfile driver,
        Vehicle vehicle,
        Dictionary<Guid, Fleet> fleetsById)
    {
        if (vehicle.FleetId is Guid fleetId && fleetsById.TryGetValue(fleetId, out Fleet? fleet))
        {
            string code = string.IsNullOrWhiteSpace(fleet.PaystackSubaccountCode)
                ? $"FLEET_{fleet.Id:N}"[..16]
                : fleet.PaystackSubaccountCode;
            return (fleet.OwnerUserId, code);
        }

        return (driver.UserId, $"DRV_{driver.Id:N}"[..16]);
    }

    private static DateOnly ParseBillingMonthStart(string billingYearMonth) =>
        DateOnly.ParseExact($"{billingYearMonth}-01", "yyyy-MM-dd", CultureInfo.InvariantCulture);
}
