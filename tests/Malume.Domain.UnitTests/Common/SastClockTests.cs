using Malume.Domain.Common;
using Shouldly;

namespace Malume.Domain.UnitTests.Common;

public sealed class SastClockTests
{
    [Fact]
    public void TodaySast_ShouldRollOverAt2200Utc()
    {
        var utc = new DateTimeOffset(2026, 11, 27, 22, 30, 0, TimeSpan.Zero);

        DateOnly sastDate = SastClock.TodaySast(utc);

        sastDate.ShouldBe(new DateOnly(2026, 11, 28));
    }
}
