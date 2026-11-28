using Malume.Application.Abstractions.Jobs;
using Malume.Application.Abstractions.Persistence;
using Malume.Domain.Common;
using Malume.Domain.Entities;
using Malume.Domain.Enums;
using Malume.Domain.Services;
using Malume.Shared.Contracts.Compliance;
using Microsoft.EntityFrameworkCore;

namespace Malume.Application.Features.Compliance;

public sealed class ComplianceExpiryScanner(
    IMalumeDbContext dbContext,
    TimeProvider timeProvider,
    EscrowFreezeService escrowFreezeService) : IComplianceExpiryScanner
{
    private readonly IMalumeDbContext _dbContext = dbContext;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly EscrowFreezeService _escrowFreezeService = escrowFreezeService;

    public async Task<ComplianceScanResultDto> ScanAsync(CancellationToken cancellationToken)
    {
        DateOnly today = SastClock.TodaySast(_timeProvider.GetUtcNow());

        List<DriverProfile> drivers = await _dbContext.DriverProfiles
            .Where(d => d.ComplianceStatus == DriverComplianceStatus.ActiveCompliant
                || d.ComplianceStatus == DriverComplianceStatus.ActiveExpiringSoon)
            .ToListAsync(cancellationToken);

        List<Guid> driverIds = drivers.Select(d => d.Id).ToList();

        List<Vehicle> assignedVehicles = driverIds.Count == 0
            ? []
            : await _dbContext.Vehicles
                .Where(v => v.IsActive && v.AssignedDriverId != null && driverIds.Contains(v.AssignedDriverId.Value))
                .ToListAsync(cancellationToken);

        Dictionary<Guid, List<Vehicle>> vehiclesByDriver = assignedVehicles
            .GroupBy(v => v.AssignedDriverId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        int markedExpiringSoon = 0;
        int suspended = 0;
        int subscriptionsPaused = 0;

        foreach (DriverProfile driver in drivers)
        {
            var expiryDates = new List<DateOnly?>
            {
                driver.PrdpExpiryDate,
                driver.PoliceClearanceExpiryDate
            };

            if (vehiclesByDriver.TryGetValue(driver.Id, out List<Vehicle>? driverVehicles))
            {
                foreach (Vehicle vehicle in driverVehicles)
                {
                    expiryDates.Add(vehicle.LicenseDiscExpiryDate);
                    expiryDates.Add(vehicle.CofExpiryDate);
                }
            }

            DateOnly? earliestExpiry = ComplianceEvaluator.EarliestExpiry([.. expiryDates]);
            DriverComplianceStatus? nextStatus = ComplianceEvaluator.EvaluateScheduledTransition(
                driver.ComplianceStatus,
                earliestExpiry,
                today);

            if (nextStatus == DriverComplianceStatus.ActiveExpiringSoon)
            {
                driver.MarkExpiringSoon();
                markedExpiringSoon++;
            }
            else if (nextStatus == DriverComplianceStatus.SuspendedExpiredDoc)
            {
                driver.SuspendForExpiredDocument();
                suspended++;
                subscriptionsPaused += await _escrowFreezeService.PauseActiveSubscriptionsForDriverAsync(
                    driver.Id,
                    cancellationToken);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ComplianceScanResultDto(
            today,
            drivers.Count,
            markedExpiringSoon,
            suspended,
            subscriptionsPaused);
    }
}
