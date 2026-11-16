using Malume.Domain.Enums;
using Malume.Domain.Exceptions;

namespace Malume.Domain.StateMachines;

public static class EscrowMilestoneStateMachine
{
    public static bool CanTransition(MilestoneStatus current, MilestoneStatus next) =>
        (current, next) switch
        {
            (MilestoneStatus.Pending, MilestoneStatus.Released) => true,
            (MilestoneStatus.Pending, MilestoneStatus.Frozen) => true,
            (MilestoneStatus.Frozen, MilestoneStatus.Pending) => true,
            (MilestoneStatus.Frozen, MilestoneStatus.Refunded) => true,
            _ when current == next => true,
            _ => false
        };

    public static MilestoneStatus Transition(MilestoneStatus current, MilestoneStatus next)
    {
        if (!CanTransition(current, next))
        {
            throw new DomainException($"Invalid escrow milestone transition from '{current}' to '{next}'.");
        }

        return next;
    }
}
