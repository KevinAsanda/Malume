using Malume.Domain.Common;
using Malume.Domain.Enums;
using Malume.Domain.Exceptions;
using Malume.Domain.ValueObjects;

namespace Malume.Domain.Entities;

public sealed class Subscription : AuditableEntity
{
    private readonly List<EscrowMilestone> _milestones = [];

    private Subscription()
    {
    }

    private Subscription(
        Guid parentUserId,
        Guid learnerId,
        Guid routeId,
        string billingYearMonth,
        ZarMoney totalAmount,
        string? paystackReference)
    {
        ParentUserId = parentUserId;
        LearnerId = learnerId;
        RouteId = routeId;
        BillingYearMonth = billingYearMonth;
        TotalAmount = totalAmount;
        EscrowStatus = EscrowStatus.Active;
        PaystackReference = paystackReference;
    }

    public Guid ParentUserId { get; private set; }

    public Guid LearnerId { get; private set; }

    public Guid RouteId { get; private set; }

    public string BillingYearMonth { get; private set; } = string.Empty;

    public ZarMoney TotalAmount { get; private set; } = ZarMoney.Zero;

    public EscrowStatus EscrowStatus { get; private set; }

    public string? PaystackReference { get; private set; }

    public IReadOnlyCollection<EscrowMilestone> Milestones => _milestones.AsReadOnly();

    public static Subscription CreateWithStagedEscrow(
        Guid parentUserId,
        Guid learnerId,
        Guid routeId,
        string billingYearMonth,
        ZarMoney totalAmount,
        DateOnly billingMonthStart,
        string? paystackReference = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(billingYearMonth);
        ArgumentNullException.ThrowIfNull(totalAmount);

        if (totalAmount.Amount <= 0m)
        {
            throw new DomainException("Subscription total amount must be greater than zero.");
        }

        var subscription = new Subscription(
            parentUserId,
            learnerId,
            routeId,
            billingYearMonth.Trim(),
            totalAmount,
            paystackReference?.Trim());

        ZarMoney tranche1 = totalAmount.Percentage(40m);
        ZarMoney tranche2 = totalAmount.Percentage(30m);
        ZarMoney tranche3 = totalAmount - tranche1 - tranche2;

        DateOnly midMonthDate = billingMonthStart.AddDays(14);
        DateOnly monthEndDate = new(
            billingMonthStart.Year,
            billingMonthStart.Month,
            DateTime.DaysInMonth(billingMonthStart.Year, billingMonthStart.Month));

        subscription._milestones.Add(new EscrowMilestone(subscription.Id, 1, 40m, tranche1, billingMonthStart));
        subscription._milestones.Add(new EscrowMilestone(subscription.Id, 2, 30m, tranche2, midMonthDate));
        subscription._milestones.Add(new EscrowMilestone(subscription.Id, 3, 30m, tranche3, monthEndDate));

        return subscription;
    }

    public void PauseForComplianceOrServiceFailure()
    {
        EscrowStatus = EscrowStatus.PausedComplianceOrServiceFailure;
        foreach (EscrowMilestone milestone in _milestones.Where(m => m.Status == MilestoneStatus.Pending))
        {
            milestone.Freeze();
        }

        MarkUpdated();
    }

    public void SetPaystackReference(string paystackReference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(paystackReference);
        PaystackReference = paystackReference.Trim();
        MarkUpdated();
    }

    public void ResumeFromComplianceFix()
    {
        EscrowStatus = EscrowStatus.Active;
        foreach (EscrowMilestone milestone in _milestones.Where(m => m.Status == MilestoneStatus.Frozen))
        {
            milestone.Unfreeze();
        }

        MarkUpdated();
    }

    public void MarkCompletedIfAllSettled()
    {
        if (_milestones.Count > 0 && _milestones.All(m => m.Status is MilestoneStatus.Released or MilestoneStatus.Refunded))
        {
            EscrowStatus = EscrowStatus.Completed;
            MarkUpdated();
        }
    }
}
