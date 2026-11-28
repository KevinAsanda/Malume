using Malume.Application.Features.Trips;
using Malume.Domain.Entities;
using Malume.Domain.Enums;
using Malume.Domain.ValueObjects;
using Malume.Shared.Contracts.Trips;
using Shouldly;

namespace Malume.Application.Tests.Features.Trips;

public sealed class RouteOptimizationServiceTests
{
    [Fact]
    public void MarkAbsentAndRecompute_ShouldEmitEtaCascadeForDownstreamLearnerSavedAtLeastTwoMinutes()
    {
        var schoolId = Guid.NewGuid();
        var tripId = Guid.NewGuid();
        var parent1 = Guid.NewGuid();
        var parent2 = Guid.NewGuid();

        var absentLearner = new Learner(
            parent1,
            schoolId,
            "Thabo Mokoena",
            "Grade 4",
            new GeoCoordinate(-26.245, 27.900),
            "1 Vilakazi St",
            "+27820000001");

        var downstreamLearner = new Learner(
            parent2,
            schoolId,
            "Amahle Dlamini",
            "Grade 5",
            new GeoCoordinate(-26.265, 27.900),
            "99 Hector Pietersen St",
            "+27820000002");

        var stop1 = new TripStopManifest(tripId, absentLearner.Id, BookingType.MonthlyContract, 1, absentLearner.HomePickupPoint);
        var stop2 = new TripStopManifest(tripId, downstreamLearner.Id, BookingType.MonthlyContract, 2, downstreamLearner.HomePickupPoint);

        var origin = new GeoCoordinate(-26.235, 27.900);
        var startUtc = new DateTimeOffset(2026, 11, 28, 4, 15, 0, TimeSpan.Zero);
        List<TripStopManifest> allStops = [stop1, stop2];
        Dictionary<Guid, Learner> learners = new()
        {
            [absentLearner.Id] = absentLearner,
            [downstreamLearner.Id] = downstreamLearner
        };

        RouteOptimizationService.EnsureBaselineEtas(origin, startUtc, allStops);
        stop1.MarkAbsent();

        IReadOnlyList<EtaCascadeUpdateDto> updates = RouteOptimizationService.RecomputeDownstreamEtas(origin, startUtc, allStops, learners);

        updates.Count.ShouldBe(1);
        updates[0].LearnerId.ShouldBe(downstreamLearner.Id);
        updates[0].ParentUserId.ShouldBe(parent2);
        updates[0].NewSequenceOrder.ShouldBe(1);
        updates[0].MinutesEarlier.ShouldBeGreaterThanOrEqualTo(3);
    }
}
