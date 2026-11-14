namespace Malume.Domain.ValueObjects;

public sealed record GeoCoordinate
{
    public double Latitude { get; }
    public double Longitude { get; }

    public GeoCoordinate(double latitude, double longitude)
    {
        if (latitude is < -90.0 or > 90.0)
        {
            throw new ArgumentOutOfRangeException(nameof(latitude), latitude, "Latitude must be between -90 and 90 degrees.");
        }

        if (longitude is < -180.0 or > 180.0)
        {
            throw new ArgumentOutOfRangeException(nameof(longitude), longitude, "Longitude must be between -180 and 180 degrees.");
        }

        Latitude = latitude;
        Longitude = longitude;
    }
}
