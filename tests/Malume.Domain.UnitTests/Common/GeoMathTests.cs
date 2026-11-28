using Malume.Domain.Common;
using Malume.Domain.ValueObjects;
using Shouldly;

namespace Malume.Domain.UnitTests.Common;

public sealed class GeoMathTests
{
    [Fact]
    public void DistanceMeters_SamePoint_ShouldBeZero()
    {
        var point = new GeoCoordinate(-26.2358, 27.9082);
        GeoMath.DistanceMeters(point, point).ShouldBe(0.0);
    }

    [Fact]
    public void DistanceMeters_SowetoPointsAboutOneKmApart_ShouldBeAccurate()
    {
        var start = new GeoCoordinate(-26.2450, 27.9000);
        var northPoint = new GeoCoordinate(-26.2360, 27.9000);

        double meters = GeoMath.DistanceMeters(start, northPoint);

        meters.ShouldBeInRange(980.0, 1020.0);
    }

    [Theory]
    [InlineData(4, 30, true)]   // 06:30 SAST
    [InlineData(6, 0, true)]    // 08:00 SAST
    [InlineData(11, 30, true)]  // 13:30 SAST
    [InlineData(13, 30, true)]  // 15:30 SAST
    [InlineData(8, 30, false)]  // 10:30 SAST (off-peak)
    [InlineData(16, 0, false)]  // 18:00 SAST (off-peak)
    public void IsPeakSchoolWindow_ShouldEvaluateSastWindows(int utcHour, int utcMinute, bool expectedPeak)
    {
        var utc = new DateTimeOffset(2026, 11, 28, utcHour, utcMinute, 0, TimeSpan.Zero);
        GeoMath.IsPeakSchoolWindow(utc).ShouldBe(expectedPeak);
    }
}
