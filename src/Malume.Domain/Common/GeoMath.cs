using Malume.Domain.ValueObjects;

namespace Malume.Domain.Common;

public static class GeoMath
{
    public const double EarthRadiusMeters = 6_371_000.0;
    public const double PeakCorridorRadiusMeters = 1_500.0;
    public const double OffPeakSearchRadiusMeters = 5_000.0;
    public const double ApproachingGateRadiusMeters = 2_000.0;

    private static readonly TimeSpan SastOffset = TimeSpan.FromHours(2);

    public static double DistanceMeters(GeoCoordinate from, GeoCoordinate to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        double lat1Rad = DegreesToRadians(from.Latitude);
        double lat2Rad = DegreesToRadians(to.Latitude);
        double deltaLat = DegreesToRadians(to.Latitude - from.Latitude);
        double deltaLon = DegreesToRadians(to.Longitude - from.Longitude);

        double a = (Math.Sin(deltaLat / 2.0) * Math.Sin(deltaLat / 2.0))
            + (Math.Cos(lat1Rad) * Math.Cos(lat2Rad)
               * Math.Sin(deltaLon / 2.0) * Math.Sin(deltaLon / 2.0));
        double c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
        return EarthRadiusMeters * c;
    }

    public static double EstimateTravelMinutes(double distanceMeters, double averageSpeedKph = 30.0)
    {
        if (distanceMeters <= 0.0 || averageSpeedKph <= 0.0)
        {
            return 0.0;
        }

        double kilometers = distanceMeters / 1_000.0;
        return (kilometers / averageSpeedKph) * 60.0;
    }

    public static bool IsPeakSchoolWindow(DateTimeOffset utcTimestamp)
    {
        TimeSpan sastTime = utcTimestamp.ToOffset(SastOffset).TimeOfDay;
        bool isMorningPeak = sastTime >= new TimeSpan(6, 0, 0) && sastTime <= new TimeSpan(8, 0, 0);
        bool isAfternoonPeak = sastTime >= new TimeSpan(13, 30, 0) && sastTime <= new TimeSpan(15, 30, 0);
        return isMorningPeak || isAfternoonPeak;
    }

    private static double DegreesToRadians(double degrees) => degrees * (Math.PI / 180.0);
}
