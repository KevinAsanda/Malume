using Malume.Application.Features.Learners;
using Malume.Application.Features.Routes;
using Malume.Application.Features.Trips;
using Malume.Shared.Contracts.Routes;
using Malume.Shared.Contracts.Trips;
using Shouldly;

namespace Malume.Application.Tests.Features.Trips;

public sealed class Phase3ValidatorTests
{
    [Fact]
    public void CreateLearnerValidator_WhenInvalidCoordinates_ShouldFail()
    {
        var validator = new CreateLearnerCommandValidator();
        var cmd = new CreateLearnerCommand(
            Guid.NewGuid(),
            "Thabo Mokoena",
            "Grade 4",
            -95.0,
            28.0,
            "12 Vilakazi St, Soweto",
            "+27821234567");

        var result = validator.Validate(cmd);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateLearnerCommand.HomeLatitude));
    }

    [Fact]
    public void CreateScheduledRouteValidator_WhenFewerThanTwoWaypoints_ShouldFail()
    {
        var validator = new CreateScheduledRouteCommandValidator();
        var cmd = new CreateScheduledRouteCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Soweto Morning Shuttle",
            "MorningToSchool",
            [new GeoPointDto(-26.236, 27.908)],
            1200m);

        var result = validator.Validate(cmd);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateScheduledRouteCommand.CorridorWaypoints));
    }

    [Fact]
    public void BatchSyncHandoversValidator_WhenInvalidActionOrMethod_ShouldFail()
    {
        var validator = new BatchSyncHandoversCommandValidator();
        var cmd = new BatchSyncHandoversCommand(
            Guid.NewGuid(),
            [
                new HandoverEventItemRequest(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "MarkedAbsent",
                    "None",
                    DateTimeOffset.UtcNow,
                    -26.20,
                    28.04,
                    true)
            ]);

        var result = validator.Validate(cmd);

        result.IsValid.ShouldBeFalse();
        result.Errors.Count.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void GetLearnerDailyPinValidator_WhenValidDirection_ShouldPass()
    {
        var validator = new GetLearnerDailyPinQueryValidator();
        var query = new GetLearnerDailyPinQuery(Guid.NewGuid(), new DateOnly(2026, 11, 28), "AfternoonFromSchool");

        var result = validator.Validate(query);

        result.IsValid.ShouldBeTrue();
    }
}
