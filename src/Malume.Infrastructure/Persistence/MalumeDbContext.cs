using Malume.Application.Abstractions.Persistence;
using Malume.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Malume.Infrastructure.Persistence;

public sealed class MalumeDbContext(DbContextOptions<MalumeDbContext> options)
    : DbContext(options), IMalumeDbContext
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Fleet> Fleets => Set<Fleet>();

    public DbSet<School> Schools => Set<School>();

    public DbSet<DriverProfile> DriverProfiles => Set<DriverProfile>();

    public DbSet<ComplianceDocument> ComplianceDocuments => Set<ComplianceDocument>();

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    public DbSet<SchoolEndorsement> SchoolEndorsements => Set<SchoolEndorsement>();

    public DbSet<Learner> Learners => Set<Learner>();

    public DbSet<ScheduledRoute> ScheduledRoutes => Set<ScheduledRoute>();

    public DbSet<DailyTripInstance> DailyTripInstances => Set<DailyTripInstance>();

    public DbSet<TripStopManifest> TripStopManifests => Set<TripStopManifest>();

    public DbSet<Subscription> Subscriptions => Set<Subscription>();

    public DbSet<EscrowMilestone> EscrowMilestones => Set<EscrowMilestone>();

    public DbSet<WalletLedgerEntry> WalletLedgerEntries => Set<WalletLedgerEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MalumeDbContext).Assembly);
    }
}
