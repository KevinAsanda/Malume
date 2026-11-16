namespace Malume.Domain.Enums;

public enum EscrowStatus
{
    Active = 1,
    PausedComplianceOrServiceFailure = 2,
    Completed = 3,
    Disputed = 4
}

public enum MilestoneStatus
{
    Pending = 1,
    Released = 2,
    Frozen = 3,
    Refunded = 4
}

public enum LedgerEntryType
{
    Debit = 1,
    Credit = 2
}

public enum LedgerReferenceType
{
    SubscriptionCharge = 1,
    MilestoneRelease40 = 2,
    MilestoneRelease30Mid = 3,
    MilestoneRelease30End = 4,
    AdHocRidePayout = 5,
    ProRataRefund = 6
}
