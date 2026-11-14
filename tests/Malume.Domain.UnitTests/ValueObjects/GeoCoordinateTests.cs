using Malume.Domain.ValueObjects;
using Shouldly;

namespace Malume.Domain.UnitTests.ValueObjects;

public sealed class GeoCoordinateTests
{
    [Fact]
    public void Constructor_WithValidSouthAfricanCoordinates_ShouldCreateCoordinate()
    {
        GeoCoordinate coordinate = new(-26.2041, 28.0473);

        coordinate.Latitude.ShouldBe(-26.2041);
        coordinate.Longitude.ShouldBe(28.0473);
    }

    [Theory]
    [InlineData(-90.1, 28.0)]
    [InlineData(90.1, 28.0)]
    public void Constructor_WithInvalidLatitude_ShouldThrowArgumentOutOfRangeException(double latitude, double longitude)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new GeoCoordinate(latitude, longitude));
    }

    [Theory]
    [InlineData(-26.0, -180.1)]
    [InlineData(-26.0, 180.1)]
    public void Constructor_WithInvalidLongitude_ShouldThrowArgumentOutOfRangeException(double latitude, double longitude)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new GeoCoordinate(latitude, longitude));
    }
}
