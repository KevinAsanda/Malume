using Malume.Domain.Enums;

namespace Malume.Domain.Services;

public static class ComplianceEvaluator
{
    public static DateOnly? EarliestExpiry(params DateOnly?[] dates)
    {
        ArgumentNullException.ThrowIfNull(dates);

        DateOnly? earliest = null;
        foreach (DateOnly? date in dates)
        {
            if (date is DateOnly value && (earliest is null || value < earliest.Value))
            {
                earliest = value;
            }
        }

        return earliest;
    }

    public static DriverComplianceStatus? EvaluateScheduledTransition(
        DriverComplianceStatus current,
        DateOnly? earliestExpiry,
        DateOnly todaySast,
        int warningWindowDays = 30)
    {
        if (current is not (DriverComplianceStatus.ActiveCompliant or DriverComplianceStatus.ActiveExpiringSoon)
            || earliestExpiry is not DateOnly expiry)
        {
            return null;
        }

        if (expiry <= todaySast)
        {
            return DriverComplianceStatus.SuspendedExpiredDoc;
        }

        if (expiry <= todaySast.AddDays(warningWindowDays) && current == DriverComplianceStatus.ActiveCompliant)
        {
            return DriverComplianceStatus.ActiveExpiringSoon;
        }

        return null;
    }
}
