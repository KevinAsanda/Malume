using Malume.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Malume.Application.Abstractions.Persistence;

public interface IMalumeDbContext
{
    DbSet<User> Users { get; }

    DbSet<Fleet> Fleets { get; }

    DbSet<School> Schools { get; }

    DbSet<DriverProfile> DriverProfiles { get; }

    DbSet<ComplianceDocument> ComplianceDocuments { get; }

    DbSet<Vehicle> Vehicles { get; }

    DbSet<SchoolEndorsement> SchoolEndorsements { get; }

    DbSet<Learner> Learners { get; }

    DbSet<ScheduledRoute> ScheduledRoutes { get; }

    DbSet<DailyTripInstance> DailyTripInstances { get; }

    DbSet<TripStopManifest> TripStopManifests { get; }

    DbSet<Subscription> Subscriptions { get; }

    DbSet<EscrowMilestone> EscrowMilestones { get; }

    DbSet<WalletLedgerEntry> WalletLedgerEntries { get; }

    DbSet<SosIncident> SosIncidents { get; }

    DbSet<UserDeviceToken> UserDeviceTokens { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
