using Malume.Domain.Entities;
using Malume.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Malume.Infrastructure.Persistence.Configurations;

internal sealed class FleetConfiguration : IEntityTypeConfiguration<Fleet>
{
    public void Configure(EntityTypeBuilder<Fleet> builder)
    {
        builder.ToTable("Tenants_Fleets");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Name).HasMaxLength(200).IsRequired();
        builder.Property(f => f.CompanyRegistrationNumber).HasMaxLength(64);
        builder.Property(f => f.PaystackSubaccountCode).HasMaxLength(100);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(f => f.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SchoolConfiguration : IEntityTypeConfiguration<School>
{
    public void Configure(EntityTypeBuilder<School> builder)
    {
        builder.ToTable("Tenants_Schools");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();
        builder.Property(s => s.EmisNumber).HasMaxLength(64);
        builder.Property(s => s.AddressText).HasMaxLength(400).IsRequired();
        builder.Property(s => s.SchoolGateLocation)
            .HasConversion<GeoCoordinateToPointConverter>()
            .HasColumnType("geography")
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(s => s.AdminUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
