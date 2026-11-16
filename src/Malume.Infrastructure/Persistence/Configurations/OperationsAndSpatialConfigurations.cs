using Malume.Domain.Entities;
using Malume.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Malume.Infrastructure.Persistence.Configurations;

internal sealed class LearnerConfiguration : IEntityTypeConfiguration<Learner>
{
    public void Configure(EntityTypeBuilder<Learner> builder)
    {
        builder.ToTable("Learners");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id).HasColumnName("LearnerId");
        builder.Property(l => l.FullName).HasMaxLength(200).IsRequired();
        builder.Property(l => l.Grade).HasMaxLength(32).IsRequired();
        builder.Property(l => l.HomeAddressText).HasMaxLength(400).IsRequired();
        builder.Property(l => l.EmergencyContactPhone).HasMaxLength(32).IsRequired();

        builder.Property(l => l.HomePickupPoint)
            .HasConversion<GeoCoordinateToPointConverter>()
            .HasColumnType("geography")
            .IsRequired();

        builder.Property<byte[]>("_qrSecretSalt")
            .HasColumnName("QrSecretSalt")
            .HasColumnType("varbinary(32)")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(l => l.ParentUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<School>()
            .WithMany()
            .HasForeignKey(l => l.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ScheduledRouteConfiguration : IEntityTypeConfiguration<ScheduledRoute>
{
    public void Configure(EntityTypeBuilder<ScheduledRoute> builder)
    {
        builder.ToTable("ScheduledRoutes");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("RouteId");
        builder.Property(r => r.RouteName).HasMaxLength(200).IsRequired();
        builder.Property(r => r.Direction).HasConversion<string>().HasMaxLength(32).IsRequired();

        builder.Property(r => r.CorridorPath)
            .HasConversion<GeoCorridorToLineStringConverter>()
            .HasColumnType("geography")
            .IsRequired();

        builder.Property(r => r.MonthlyPrice)
            .HasColumnName("MonthlyPriceZar")
            .HasConversion<ZarMoneyToDecimalConverter>()
            .HasPrecision(18, 2)
            .IsRequired();

        builder.HasOne<Vehicle>()
            .WithMany()
            .HasForeignKey(r => r.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<DriverProfile>()
            .WithMany()
            .HasForeignKey(r => r.DriverProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<School>()
            .WithMany()
            .HasForeignKey(r => r.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DailyTripInstanceConfiguration : IEntityTypeConfiguration<DailyTripInstance>
{
    public void Configure(EntityTypeBuilder<DailyTripInstance> builder)
    {
        builder.ToTable("DailyTripInstances");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("TripId");
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(32).IsRequired();

        builder.HasIndex(t => new { t.RouteId, t.ServiceDate });

        builder.HasOne<ScheduledRoute>()
            .WithMany()
            .HasForeignKey(t => t.RouteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<DriverProfile>()
            .WithMany()
            .HasForeignKey(t => t.DriverProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Vehicle>()
            .WithMany()
            .HasForeignKey(t => t.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class TripStopManifestConfiguration : IEntityTypeConfiguration<TripStopManifest>
{
    public void Configure(EntityTypeBuilder<TripStopManifest> builder)
    {
        builder.ToTable("TripStopManifests");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id).HasColumnName("ManifestEntryId");
        builder.Property(m => m.BookingType).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(m => m.AttendanceStatus).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(m => m.HandoverMethod).HasConversion<string>().HasMaxLength(32).IsRequired();

        builder.Property(m => m.StopLocation)
            .HasConversion<GeoCoordinateToPointConverter>()
            .HasColumnType("geography")
            .IsRequired();

        builder.Property(m => m.HandoverGpsPoint)
            .HasConversion<GeoCoordinateToPointConverter>()
            .HasColumnType("geography");

        builder.HasIndex(m => m.OfflineEventId)
            .IsUnique()
            .HasFilter("[OfflineEventId] IS NOT NULL");

        builder.HasOne<DailyTripInstance>()
            .WithMany()
            .HasForeignKey(m => m.TripId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Learner>()
            .WithMany()
            .HasForeignKey(m => m.LearnerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
