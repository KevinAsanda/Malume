using Malume.Domain.Exceptions;

namespace Malume.Domain.ValueObjects;

public sealed class GeoCorridor
{
    public IReadOnlyList<GeoCoordinate> Points { get; }

    public GeoCorridor(IEnumerable<GeoCoordinate> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        List<GeoCoordinate> materialized = points.ToList();
        if (materialized.Count < 2)
        {
            throw new DomainException("A route corridor must contain at least two GPS coordinates.");
        }

        Points = materialized.AsReadOnly();
    }
}
