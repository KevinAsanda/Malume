using Malume.Domain.Enums;
using Malume.Domain.Exceptions;

namespace Malume.Domain.StateMachines;

public static class DriverComplianceStateMachine
{
    public static bool CanTransition(DriverComplianceStatus current, DriverComplianceStatus next) =>
        (current, next) switch
        {
            (DriverComplianceStatus.PendingReview, DriverComplianceStatus.ActiveCompliant) => true,
            (DriverComplianceStatus.PendingReview, DriverComplianceStatus.SuspendedSafetyIncident) => true,
            (DriverComplianceStatus.ActiveCompliant, DriverComplianceStatus.ActiveExpiringSoon) => true,
            (DriverComplianceStatus.ActiveCompliant, DriverComplianceStatus.SuspendedExpiredDoc) => true,
            (DriverComplianceStatus.ActiveCompliant, DriverComplianceStatus.SuspendedSafetyIncident) => true,
            (DriverComplianceStatus.ActiveExpiringSoon, DriverComplianceStatus.ActiveCompliant) => true,
            (DriverComplianceStatus.ActiveExpiringSoon, DriverComplianceStatus.SuspendedExpiredDoc) => true,
            (DriverComplianceStatus.ActiveExpiringSoon, DriverComplianceStatus.SuspendedSafetyIncident) => true,
            (DriverComplianceStatus.SuspendedExpiredDoc, DriverComplianceStatus.ActiveCompliant) => true,
            (DriverComplianceStatus.SuspendedSafetyIncident, DriverComplianceStatus.ActiveCompliant) => true,
            _ when current == next => true,
            _ => false
        };

    public static DriverComplianceStatus Transition(DriverComplianceStatus current, DriverComplianceStatus next)
    {
        if (!CanTransition(current, next))
        {
            throw new DomainException($"Invalid driver compliance state transition from '{current}' to '{next}'.");
        }

        return next;
    }

    public static bool CanStartTrip(DriverComplianceStatus status) =>
        status is DriverComplianceStatus.ActiveCompliant or DriverComplianceStatus.ActiveExpiringSoon;
}
