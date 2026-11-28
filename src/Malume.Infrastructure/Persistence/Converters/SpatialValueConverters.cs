using Malume.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace Malume.Infrastructure.Persistence.Converters;

public static class SpatialGeometryDefaults
{
    public const int Wgs84Srid = 4326;

    public static GeometryFactory GeometryFactory { get; } =
        NtsGeometryServices.Instance.CreateGeometryFactory(srid: Wgs84Srid);
}

public sealed class GeoCoordinateToPointConverter() : ValueConverter<GeoCoordinate, Point>(
    coordinate => SpatialGeometryDefaults.GeometryFactory.CreatePoint(
        new Coordinate(coordinate.Longitude, coordinate.Latitude)),
    point => new GeoCoordinate(point.Y, point.X));

public sealed class GeoCorridorToLineStringConverter() : ValueConverter<GeoCorridor, LineString>(
    corridor => SpatialGeometryDefaults.GeometryFactory.CreateLineString(
        corridor.Points.Select(p => new Coordinate(p.Longitude, p.Latitude)).ToArray()),
    lineString => new GeoCorridor(
        lineString.Coordinates.Select(c => new GeoCoordinate(c.Y, c.X))));

public sealed class ZarMoneyToDecimalConverter() : ValueConverter<ZarMoney, decimal>(
    money => money.Amount,
    amount => new ZarMoney(amount));
