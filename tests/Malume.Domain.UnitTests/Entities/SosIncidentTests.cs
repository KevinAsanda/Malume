using Malume.Domain.Entities;
using Malume.Domain.Enums;
using Malume.Domain.Exceptions;
using Malume.Domain.ValueObjects;
using Shouldly;

namespace Malume.Domain.UnitTests.Entities;

public sealed class SosIncidentTests
{
    [Fact]
    public void CreateAcknowledgeResolve_ShouldTransitionCleanly()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var incident = new SosIncident(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            UserRole.Parent,
            "Thabo Mokoena",
            "CA 123-456",
            new GeoCoordinate(-26.2041, 28.0473),
            6,
            now,
            "Vehicle stopped on shoulder");

        incident.Status.ShouldBe(SosIncidentStatus.Active);
        incident.TriggeredByRole.ShouldBe(UserRole.Parent);
        incident.AcknowledgedAtUtc.ShouldBeNull();
        incident.ResolvedAtUtc.ShouldBeNull();

        incident.Acknowledge(now.AddMinutes(1));
        incident.Status.ShouldBe(SosIncidentStatus.Acknowledged);
        incident.AcknowledgedAtUtc.ShouldBe(now.AddMinutes(1));

        Guid resolverId = Guid.NewGuid();
        incident.Resolve(resolverId, "Spare Quantum arrived on scene.", now.AddMinutes(8));
        incident.Status.ShouldBe(SosIncidentStatus.Resolved);
        incident.ResolvedByUserId.ShouldBe(resolverId);
        incident.ResolutionNotes.ShouldBe("Spare Quantum arrived on scene.");

        Should.Throw<DomainException>(() => incident.Acknowledge(now.AddMinutes(9)));
        Should.Throw<DomainException>(() => incident.Resolve(resolverId, "Again", now.AddMinutes(10)));
    }
}
