using Malume.Domain.Common;
using Malume.Domain.Entities;
using Malume.Domain.Enums;
using Malume.Domain.ValueObjects;
using Malume.Shared.Contracts.Trips;

namespace Malume.Application.Features.Trips;

public static class RouteOptimizationService
{
    private const double StopDwellMinutes = 3.0;
    private const double AverageCorridorSpeedKph = 30.0;
    private const double MinimumEtaDeltaMinutes = 2.0;

    public static void EnsureBaselineEtas(
        GeoCoordinate origin,
        DateTimeOffset departureUtc,
        IReadOnlyList<TripStopManifest> allStops)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(allStops);

        List<TripStopManifest> orderedActive = allStops
            .Where(s => s.AttendanceStatus != AttendanceStatus.MarkedAbsent)
            .OrderBy(s => s.SequenceOrder)
            .ToList();

        GeoCoordinate previousPoint = origin;
        DateTimeOffset cursorUtc = departureUtc;

        foreach (TripStopManifest stop in orderedActive)
        {
            double legMinutes = GeoMath.EstimateTravelMinutes(
                GeoMath.DistanceMeters(previousPoint, stop.StopLocation),
                AverageCorridorSpeedKph);
            cursorUtc = cursorUtc.AddMinutes(legMinutes);

            if (stop.EstimatedArrivalUtc is null)
            {
                stop.UpdateEstimatedArrival(cursorUtc);
            }
            else
            {
                cursorUtc = stop.EstimatedArrivalUtc.Value;
            }

            cursorUtc = cursorUtc.AddMinutes(StopDwellMinutes);
            previousPoint = stop.StopLocation;
        }
    }

    public static IReadOnlyList<EtaCascadeUpdateDto> RecomputeDownstreamEtas(
        GeoCoordinate origin,
        DateTimeOffset departureUtc,
        IReadOnlyList<TripStopManifest> allStops,
        IReadOnlyDictionary<Guid, Learner> learnersById)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(allStops);
        ArgumentNullException.ThrowIfNull(learnersById);

        List<TripStopManifest> orderedActive = allStops
            .Where(s => s.AttendanceStatus != AttendanceStatus.MarkedAbsent)
            .OrderBy(s => s.SequenceOrder)
            .ToList();

        GeoCoordinate previousPoint = origin;
        DateTimeOffset cursorUtc = departureUtc;
        List<EtaCascadeUpdateDto> cascadeUpdates = [];

        for (int index = 0; index < orderedActive.Count; index++)
        {
            TripStopManifest stop = orderedActive[index];
            int newSequenceOrder = index + 1;
            if (stop.SequenceOrder != newSequenceOrder)
            {
                stop.UpdateSequenceOrder(newSequenceOrder);
            }

            double legMinutes = GeoMath.EstimateTravelMinutes(
                GeoMath.DistanceMeters(previousPoint, stop.StopLocation),
                AverageCorridorSpeedKph);
            DateTimeOffset newEtaUtc = cursorUtc.AddMinutes(legMinutes);
            DateTimeOffset? previousEtaUtc = stop.EstimatedArrivalUtc;

            stop.UpdateEstimatedArrival(newEtaUtc);

            if (previousEtaUtc.HasValue && stop.AttendanceStatus == AttendanceStatus.Scheduled)
            {
                double minutesEarlierRaw = (previousEtaUtc.Value - newEtaUtc).TotalMinutes;
                if (minutesEarlierRaw >= MinimumEtaDeltaMinutes && learnersById.TryGetValue(stop.LearnerId, out Learner? learner))
                {
                    cascadeUpdates.Add(new EtaCascadeUpdateDto(
                        learner.Id,
                        learner.ParentUserId,
                        learner.FullName,
                        newSequenceOrder,
                        previousEtaUtc,
                        newEtaUtc,
                        (int)Math.Round(minutesEarlierRaw, MidpointRounding.AwayFromZero)));
                }
            }

            cursorUtc = newEtaUtc.AddMinutes(StopDwellMinutes);
            previousPoint = stop.StopLocation;
        }

        return cascadeUpdates;
    }

    public static int ComputeInsertionSequenceOrder(
        IReadOnlyList<TripStopManifest> existingStops,
        GeoCoordinate newStopLocation)
    {
        ArgumentNullException.ThrowIfNull(existingStops);
        ArgumentNullException.ThrowIfNull(newStopLocation);

        List<TripStopManifest> activeStops = existingStops
            .Where(s => s.AttendanceStatus != AttendanceStatus.MarkedAbsent)
            .OrderBy(s => s.SequenceOrder)
            .ToList();

        if (activeStops.Count == 0)
        {
            return 1;
        }

        int bestInsertIndex = activeStops.Count;
        double bestPenalty = double.MaxValue;

        for (int idx = 0; idx <= activeStops.Count; idx++)
        {
            double detour = idx switch
            {
                0 => GeoMath.DistanceMeters(newStopLocation, activeStops[0].StopLocation),
                int tail when tail == activeStops.Count => GeoMath.DistanceMeters(activeStops[^1].StopLocation, newStopLocation),
                _ => GeoMath.DistanceMeters(activeStops[idx - 1].StopLocation, newStopLocation)
                     + GeoMath.DistanceMeters(newStopLocation, activeStops[idx].StopLocation)
                     - GeoMath.DistanceMeters(activeStops[idx - 1].StopLocation, activeStops[idx].StopLocation)
            };

            if (detour < bestPenalty)
            {
                bestPenalty = detour;
                bestInsertIndex = idx;
            }
        }

        return bestInsertIndex + 1;
    }
}
