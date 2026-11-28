using Malume.Application.Abstractions.Persistence;
using Malume.Domain.Entities;
using Malume.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Malume.Application.Features.Compliance;

public sealed class EscrowFreezeService(IMalumeDbContext dbContext)
{
    private readonly IMalumeDbContext _dbContext = dbContext;

    public async Task<int> PauseActiveSubscriptionsForDriverAsync(
        Guid driverProfileId,
        CancellationToken cancellationToken)
    {
        List<Guid> routeIds = await _dbContext.ScheduledRoutes
            .Where(r => r.DriverProfileId == driverProfileId && r.IsActive)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        if (routeIds.Count == 0)
        {
            return 0;
        }

        List<Subscription> activeSubscriptions = await _dbContext.Subscriptions
            .Include(s => s.Milestones)
            .Where(s => routeIds.Contains(s.RouteId) && s.EscrowStatus == EscrowStatus.Active)
            .ToListAsync(cancellationToken);

        foreach (Subscription subscription in activeSubscriptions)
        {
            subscription.PauseForComplianceOrServiceFailure();
        }

        return activeSubscriptions.Count;
    }
}
