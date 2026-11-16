using Malume.Domain.Entities;
using Malume.Domain.Enums;
using Malume.Domain.ValueObjects;
using Shouldly;

namespace Malume.Domain.UnitTests.Entities;

public sealed class SubscriptionEscrowTests
{
    [Theory]
    [InlineData(1200.00)]
    [InlineData(999.99)]
    [InlineData(1234.55)]
    public void CreateWithStagedEscrow_ShouldSplitInto40_30_30_AndReconcileExactTotal(decimal totalZar)
    {
        var billingStart = new DateOnly(2026, 11, 1);

        Subscription subscription = Subscription.CreateWithStagedEscrow(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "2026-11",
            ZarMoney.Of(totalZar),
            billingStart,
            "paystack_ref_001");

        subscription.EscrowStatus.ShouldBe(EscrowStatus.Active);
        subscription.Milestones.Count.ShouldBe(3);

        List<EscrowMilestone> ordered = subscription.Milestones.OrderBy(m => m.TrancheNumber).ToList();
        ordered[0].Percentage.ShouldBe(40m);
        ordered[1].Percentage.ShouldBe(30m);
        ordered[2].Percentage.ShouldBe(30m);
        ordered[0].ScheduledReleaseDate.ShouldBe(new DateOnly(2026, 11, 1));
        ordered[1].ScheduledReleaseDate.ShouldBe(new DateOnly(2026, 11, 15));
        ordered[2].ScheduledReleaseDate.ShouldBe(new DateOnly(2026, 11, 30));

        decimal sum = ordered.Sum(m => m.Amount.Amount);
        sum.ShouldBe(totalZar);
    }

    [Fact]
    public void PauseForComplianceOrServiceFailure_ShouldFreezeOnlyPendingMilestones()
    {
        Subscription subscription = Subscription.CreateWithStagedEscrow(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "2026-11",
            ZarMoney.Of(1200m),
            new DateOnly(2026, 11, 1));

        List<EscrowMilestone> ordered = subscription.Milestones.OrderBy(m => m.TrancheNumber).ToList();
        ordered[0].Release("TRF_001");

        subscription.PauseForComplianceOrServiceFailure();

        subscription.EscrowStatus.ShouldBe(EscrowStatus.PausedComplianceOrServiceFailure);
        ordered[0].Status.ShouldBe(MilestoneStatus.Released);
        ordered[1].Status.ShouldBe(MilestoneStatus.Frozen);
        ordered[2].Status.ShouldBe(MilestoneStatus.Frozen);
    }
}
