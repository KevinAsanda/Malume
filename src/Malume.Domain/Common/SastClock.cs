namespace Malume.Domain.Common;

public static class SastClock
{
    public static DateOnly TodaySast(DateTimeOffset utcNow) =>
        DateOnly.FromDateTime(utcNow.UtcDateTime.AddHours(2));
}
