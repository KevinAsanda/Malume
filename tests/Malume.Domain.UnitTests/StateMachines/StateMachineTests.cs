using Malume.Domain.Enums;
using Malume.Domain.Exceptions;
using Malume.Domain.StateMachines;
using Shouldly;

namespace Malume.Domain.UnitTests.StateMachines;

public sealed class StateMachineTests
{
    [Fact]
    public void DriverComplianceStateMachine_ShouldEnforceValidTransitions()
    {
        DriverComplianceStateMachine.CanStartTrip(DriverComplianceStatus.PendingReview).ShouldBeFalse();
        DriverComplianceStateMachine.CanStartTrip(DriverComplianceStatus.ActiveCompliant).ShouldBeTrue();
        DriverComplianceStateMachine.CanStartTrip(DriverComplianceStatus.ActiveExpiringSoon).ShouldBeTrue();
        DriverComplianceStateMachine.CanStartTrip(DriverComplianceStatus.SuspendedExpiredDoc).ShouldBeFalse();

        DriverComplianceStatus next = DriverComplianceStateMachine.Transition(
            DriverComplianceStatus.PendingReview,
            DriverComplianceStatus.ActiveCompliant);
        next.ShouldBe(DriverComplianceStatus.ActiveCompliant);

        Should.Throw<DomainException>(() =>
            DriverComplianceStateMachine.Transition(
                DriverComplianceStatus.PendingReview,
                DriverComplianceStatus.SuspendedExpiredDoc));
    }

    [Fact]
    public void TripLifecycleStateMachine_ShouldBlockSkippingEnRoute()
    {
        TripLifecycleStatus enRoute = TripLifecycleStateMachine.Transition(
            TripLifecycleStatus.Scheduled,
            TripLifecycleStatus.EnRoute);
        enRoute.ShouldBe(TripLifecycleStatus.EnRoute);

        Should.Throw<DomainException>(() =>
            TripLifecycleStateMachine.Transition(
                TripLifecycleStatus.Scheduled,
                TripLifecycleStatus.Completed));
    }

    [Fact]
    public void EscrowMilestoneStateMachine_ShouldBlockRefreezingReleasedMilestone()
    {
        Should.Throw<DomainException>(() =>
            EscrowMilestoneStateMachine.Transition(
                MilestoneStatus.Released,
                MilestoneStatus.Frozen));
    }
}
