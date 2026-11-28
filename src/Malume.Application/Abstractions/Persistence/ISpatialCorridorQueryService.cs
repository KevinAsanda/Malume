using Malume.Shared.Contracts.Corridor;

namespace Malume.Application.Abstractions.Persistence;

public interface ISpatialCorridorQueryService
{
    Task<IReadOnlyList<CorridorAdHocOptionDto>> FindCorridorOptionsAsync(
        Guid targetSchoolId,
        DateOnly serviceDate,
        double pickupLatitude,
        double pickupLongitude,
        double maxDistanceMeters,
        string? direction,
        CancellationToken cancellationToken);

    Task<double?> GetDistanceToRouteCorridorMetersAsync(
        Guid routeId,
        double pickupLatitude,
        double pickupLongitude,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<OffPeakDriverOptionDto>> FindNearbyOffPeakDriversAsync(
        double pickupLatitude,
        double pickupLongitude,
        double maxDistanceMeters,
        CancellationToken cancellationToken);
}
