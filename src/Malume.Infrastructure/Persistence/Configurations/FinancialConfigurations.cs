using Malume.Domain.Entities;
using Malume.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Malume.Infrastructure.Persistence.Configurations;

internal sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.ToTable("Subscriptions");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("SubscriptionId");
        builder.Property(s => s.BillingYearMonth).HasMaxLength(7).IsFixedLength().IsRequired();
        builder.Property(s => s.EscrowStatus).HasConversion<string>().HasMaxLength(48).IsRequired();
        builder.Property(s => s.PaystackReference).HasMaxLength(128);

        builder.Property(s => s.TotalAmount)
            .HasColumnName("TotalAmountZar")
            .HasConversion<ZarMoneyToDecimalConverter>()
            .HasPrecision(18, 2)
            .IsRequired();

        builder.HasMany(s => s.Milestones)
            .WithOne()
            .HasForeignKey(m => m.SubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(s => s.Milestones).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(s => s.ParentUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Learner>()
            .WithMany()
            .HasForeignKey(s => s.LearnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ScheduledRoute>()
            .WithMany()
            .HasForeignKey(s => s.RouteId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class EscrowMilestoneConfiguration : IEntityTypeConfiguration<EscrowMilestone>
{
    public void Configure(EntityTypeBuilder<EscrowMilestone> builder)
    {
        builder.ToTable("EscrowMilestones");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Percentage).HasPrecision(5, 2).IsRequired();
        builder.Property(m => m.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(m => m.PaystackTransferCode).HasMaxLength(128);

        builder.Property(m => m.Amount)
            .HasColumnName("AmountZar")
            .HasConversion<ZarMoneyToDecimalConverter>()
            .HasPrecision(18, 2)
            .IsRequired();

        builder.HasIndex(m => new { m.SubscriptionId, m.TrancheNumber }).IsUnique();
    }
}

internal sealed class WalletLedgerEntryConfiguration : IEntityTypeConfiguration<WalletLedgerEntry>
{
    public void Configure(EntityTypeBuilder<WalletLedgerEntry> builder)
    {
        builder.ToTable("WalletLedgerEntries");
        builder.HasKey(w => w.Id);

        builder.Property(w => w.Id).HasColumnName("LedgerEntryId");
        builder.Property(w => w.EntryType).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(w => w.ReferenceType).HasConversion<string>().HasMaxLength(48).IsRequired();
        builder.Property(w => w.PaystackReference).HasMaxLength(128);
        builder.Property(w => w.Notes).HasMaxLength(500);

        builder.Property(w => w.Amount)
            .HasColumnName("AmountZar")
            .HasConversion<ZarMoneyToDecimalConverter>()
            .HasPrecision(18, 2)
            .IsRequired();

        builder.HasIndex(w => w.TransactionGroupId);
        builder.HasIndex(w => w.WalletAccountId);
    }
}
