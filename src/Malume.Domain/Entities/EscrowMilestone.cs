using Malume.Domain.Common;
using Malume.Domain.Enums;
using Malume.Domain.StateMachines;
using Malume.Domain.ValueObjects;

namespace Malume.Domain.Entities;

public sealed class EscrowMilestone : AuditableEntity
{
    private EscrowMilestone()
    {
    }

    internal EscrowMilestone(
        Guid subscriptionId,
        int trancheNumber,
        decimal percentage,
        ZarMoney amount,
        DateOnly scheduledReleaseDate)
    {
        SubscriptionId = subscriptionId;
        TrancheNumber = trancheNumber;
        Percentage = percentage;
        Amount = amount;
        ScheduledReleaseDate = scheduledReleaseDate;
        Status = MilestoneStatus.Pending;
    }

    public Guid SubscriptionId { get; private set; }

    public int TrancheNumber { get; private set; }

    public decimal Percentage { get; private set; }

    public ZarMoney Amount { get; private set; } = ZarMoney.Zero;

    public MilestoneStatus Status { get; private set; }

    public DateOnly ScheduledReleaseDate { get; private set; }

    public DateTimeOffset? ReleasedAtUtc { get; private set; }

    public string? PaystackTransferCode { get; private set; }

    public void Release(string paystackTransferCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(paystackTransferCode);

        Status = EscrowMilestoneStateMachine.Transition(Status, MilestoneStatus.Released);
        PaystackTransferCode = paystackTransferCode.Trim();
        ReleasedAtUtc = DateTimeOffset.UtcNow;
        MarkUpdated();
    }

    public void Freeze()
    {
        Status = EscrowMilestoneStateMachine.Transition(Status, MilestoneStatus.Frozen);
        MarkUpdated();
    }

    public void MarkRefunded()
    {
        Status = EscrowMilestoneStateMachine.Transition(Status, MilestoneStatus.Refunded);
        MarkUpdated();
    }
}
