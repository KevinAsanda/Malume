using Malume.Domain.Enums;
using Malume.Domain.Exceptions;

namespace Malume.Domain.StateMachines;

public static class TripLifecycleStateMachine
{
    public static bool CanTransition(TripLifecycleStatus current, TripLifecycleStatus next) =>
        (current, next) switch
        {
            (TripLifecycleStatus.Scheduled, TripLifecycleStatus.EnRoute) => true,
            (TripLifecycleStatus.Scheduled, TripLifecycleStatus.Cancelled) => true,
            (TripLifecycleStatus.EnRoute, TripLifecycleStatus.AtSchoolGate) => true,
            (TripLifecycleStatus.EnRoute, TripLifecycleStatus.Cancelled) => true,
            (TripLifecycleStatus.AtSchoolGate, TripLifecycleStatus.Completed) => true,
            _ when current == next => true,
            _ => false
        };

    public static TripLifecycleStatus Transition(TripLifecycleStatus current, TripLifecycleStatus next)
    {
        if (!CanTransition(current, next))
        {
            throw new DomainException($"Invalid trip lifecycle state transition from '{current}' to '{next}'.");
        }

        return next;
    }
}
