using Malume.Domain.Entities;
using Malume.Domain.Enums;
using Malume.Domain.Services;
using Malume.Domain.ValueObjects;
using Shouldly;

namespace Malume.Domain.UnitTests.Services;

public sealed class EscrowAccountingServiceTests
{
    [Fact]
    public void CreateSubscriptionChargeAndMilestonePairs_ShouldAlwaysBalanceDebitsAndCredits()
    {
        var amount = new ZarMoney(1200m);
        IReadOnlyList<WalletLedgerEntry> chargePair = EscrowAccountingService.CreateSubscriptionChargeEntries(
            amount,
            "MLM-REF-001");

        chargePair.Count.ShouldBe(2);
        chargePair.Select(e => e.TransactionGroupId).Distinct().Count().ShouldBe(1);
        chargePair.Where(e => e.EntryType == LedgerEntryType.Debit).Sum(e => e.Amount.Amount).ShouldBe(1200m);
        chargePair.Where(e => e.EntryType == LedgerEntryType.Credit).Sum(e => e.Amount.Amount).ShouldBe(1200m);

        Guid fleetWalletId = Guid.NewGuid();
        IReadOnlyList<WalletLedgerEntry> tranche1 = EscrowAccountingService.CreateMilestoneReleaseEntries(
            fleetWalletId,
            1,
            new ZarMoney(480m),
            "TRF_TEST_1");

        tranche1.Count.ShouldBe(2);
        tranche1.Single(e => e.WalletAccountId == fleetWalletId).EntryType.ShouldBe(LedgerEntryType.Credit);
        tranche1.Single(e => e.WalletAccountId == EscrowAccountingService.PlatformEscrowLiabilityAccountId)
            .EntryType.ShouldBe(LedgerEntryType.Debit);
    }
}
