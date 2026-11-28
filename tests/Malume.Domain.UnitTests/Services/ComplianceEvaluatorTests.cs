using Malume.Domain.Enums;
using Malume.Domain.Services;
using Shouldly;

namespace Malume.Domain.UnitTests.Services;

public sealed class ComplianceEvaluatorTests
{
    private static readonly DateOnly Today = new(2026, 11, 28);

    [Fact]
    public void EvaluateScheduledTransition_WhenExpired_ShouldReturnSuspendedExpiredDoc()
    {
        DriverComplianceStatus? result = ComplianceEvaluator.EvaluateScheduledTransition(
            DriverComplianceStatus.ActiveCompliant,
            Today.AddDays(-1),
            Today);

        result.ShouldBe(DriverComplianceStatus.SuspendedExpiredDoc);
    }

    [Fact]
    public void EvaluateScheduledTransition_WhenExpiringToday_ShouldReturnSuspendedExpiredDoc()
    {
        DriverComplianceStatus? result = ComplianceEvaluator.EvaluateScheduledTransition(
            DriverComplianceStatus.ActiveExpiringSoon,
            Today,
            Today);

        result.ShouldBe(DriverComplianceStatus.SuspendedExpiredDoc);
    }

    [Fact]
    public void EvaluateScheduledTransition_WhenActiveCompliantWithinWindow_ShouldReturnActiveExpiringSoon()
    {
        DriverComplianceStatus? result = ComplianceEvaluator.EvaluateScheduledTransition(
            DriverComplianceStatus.ActiveCompliant,
            Today.AddDays(30),
            Today);

        result.ShouldBe(DriverComplianceStatus.ActiveExpiringSoon);
    }

    [Fact]
    public void EvaluateScheduledTransition_WhenAlreadyActiveExpiringSoonWithinWindow_ShouldReturnNull()
    {
        DriverComplianceStatus? result = ComplianceEvaluator.EvaluateScheduledTransition(
            DriverComplianceStatus.ActiveExpiringSoon,
            Today.AddDays(10),
            Today);

        result.ShouldBeNull();
    }

    [Fact]
    public void EvaluateScheduledTransition_WhenMoreThan30DaysAway_ShouldReturnNull()
    {
        DriverComplianceStatus? result = ComplianceEvaluator.EvaluateScheduledTransition(
            DriverComplianceStatus.ActiveCompliant,
            Today.AddDays(31),
            Today);

        result.ShouldBeNull();
    }

    [Fact]
    public void EvaluateScheduledTransition_WhenPendingReview_ShouldReturnNull()
    {
        DriverComplianceStatus? result = ComplianceEvaluator.EvaluateScheduledTransition(
            DriverComplianceStatus.PendingReview,
            Today.AddDays(-5),
            Today);

        result.ShouldBeNull();
    }

    [Fact]
    public void EvaluateScheduledTransition_WhenNullExpiry_ShouldReturnNull()
    {
        DriverComplianceStatus? result = ComplianceEvaluator.EvaluateScheduledTransition(
            DriverComplianceStatus.ActiveCompliant,
            null,
            Today);

        result.ShouldBeNull();
    }

    [Fact]
    public void EarliestExpiry_ShouldIgnoreNullsAndReturnMinimum()
    {
        DateOnly? earliest = ComplianceEvaluator.EarliestExpiry(
            null,
            new DateOnly(2027, 5, 1),
            null,
            new DateOnly(2026, 12, 15),
            new DateOnly(2027, 1, 10));

        earliest.ShouldBe(new DateOnly(2026, 12, 15));
        ComplianceEvaluator.EarliestExpiry(null, null).ShouldBeNull();
    }
}
