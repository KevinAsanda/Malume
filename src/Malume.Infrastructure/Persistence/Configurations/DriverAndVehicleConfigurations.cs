using Malume.Domain.Entities;
using Malume.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Malume.Infrastructure.Persistence.Configurations;

internal sealed class DriverProfileConfiguration : IEntityTypeConfiguration<DriverProfile>
{
    public void Configure(EntityTypeBuilder<DriverProfile> builder)
    {
        builder.ToTable("DriverProfiles");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id).HasColumnName("DriverId");
        builder.Property(d => d.ComplianceStatus).HasConversion<string>().HasMaxLength(48).IsRequired();
        builder.Property(d => d.OperatingMode).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(d => d.PrdpNumber).HasMaxLength(64).IsRequired();
        builder.Property(d => d.CurrentLocation)
            .HasConversion<GeoCoordinateToPointConverter>()
            .HasColumnType("geography");

        builder.HasIndex(d => d.UserId).IsUnique();

        builder.HasOne<User>()
            .WithOne()
            .HasForeignKey<DriverProfile>(d => d.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Fleet>()
            .WithMany()
            .HasForeignKey(d => d.FleetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ComplianceDocumentConfiguration : IEntityTypeConfiguration<ComplianceDocument>
{
    public void Configure(EntityTypeBuilder<ComplianceDocument> builder)
    {
        builder.ToTable("ComplianceDocuments");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.DocumentType).HasConversion<string>().HasMaxLength(48).IsRequired();
        builder.Property(c => c.DocumentNumber).HasMaxLength(100).IsRequired();
        builder.Property(c => c.StorageUrl).HasMaxLength(1024).IsRequired();

        builder.HasOne<DriverProfile>()
            .WithMany()
            .HasForeignKey(c => c.DriverProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Vehicle>()
            .WithMany()
            .HasForeignKey(c => c.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(c => c.VerifiedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("Vehicles");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Id).HasColumnName("VehicleId");
        builder.Property(v => v.RegistrationNumber).HasMaxLength(32).IsRequired();
        builder.Property(v => v.MakeModel).HasMaxLength(120).IsRequired();

        builder.HasIndex(v => v.RegistrationNumber).IsUnique();

        builder.HasOne<Fleet>()
            .WithMany()
            .HasForeignKey(v => v.FleetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<School>()
            .WithMany()
            .HasForeignKey(v => v.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<DriverProfile>()
            .WithMany()
            .HasForeignKey(v => v.AssignedDriverId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class SchoolEndorsementConfiguration : IEntityTypeConfiguration<SchoolEndorsement>
{
    public void Configure(EntityTypeBuilder<SchoolEndorsement> builder)
    {
        builder.ToTable("SchoolEndorsements");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Notes).HasMaxLength(500);
        builder.HasIndex(e => new { e.SchoolId, e.DriverProfileId }).IsUnique();

        builder.HasOne<School>()
            .WithMany()
            .HasForeignKey(e => e.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<DriverProfile>()
            .WithMany()
            .HasForeignKey(e => e.DriverProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.EndorsedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
