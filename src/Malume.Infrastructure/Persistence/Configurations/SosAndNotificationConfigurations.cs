using Malume.Domain.Entities;
using Malume.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Malume.Infrastructure.Persistence.Configurations;

internal sealed class SosIncidentConfiguration : IEntityTypeConfiguration<SosIncident>
{
    public void Configure(EntityTypeBuilder<SosIncident> builder)
    {
        builder.ToTable("SosIncidents");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("SosIncidentId");
        builder.Property(s => s.TriggeredByRole).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(s => s.DriverFullName).HasMaxLength(200).IsRequired();
        builder.Property(s => s.VehicleRegistration).HasMaxLength(32).IsRequired();
        builder.Property(s => s.Notes).HasMaxLength(500);
        builder.Property(s => s.ResolutionNotes).HasMaxLength(500);

        builder.Property(s => s.Location)
            .HasConversion<GeoCoordinateToPointConverter>()
            .HasColumnType("geography")
            .IsRequired();

        builder.HasIndex(s => new { s.SchoolId, s.Status });
        builder.HasIndex(s => s.TripId);

        builder.HasOne<DailyTripInstance>()
            .WithMany()
            .HasForeignKey(s => s.TripId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<School>()
            .WithMany()
            .HasForeignKey(s => s.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<DriverProfile>()
            .WithMany()
            .HasForeignKey(s => s.DriverProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(s => s.TriggeredByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class UserDeviceTokenConfiguration : IEntityTypeConfiguration<UserDeviceToken>
{
    public void Configure(EntityTypeBuilder<UserDeviceToken> builder)
    {
        builder.ToTable("UserDeviceTokens");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("DeviceTokenId");
        builder.Property(t => t.Token).HasMaxLength(512).IsRequired();
        builder.Property(t => t.Platform).HasMaxLength(32).IsRequired();
        builder.Property(t => t.AppVariant).HasMaxLength(32).IsRequired();

        builder.HasIndex(t => new { t.UserId, t.Token }).IsUnique();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
