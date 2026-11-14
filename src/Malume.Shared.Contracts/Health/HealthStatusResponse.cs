namespace Malume.Shared.Contracts.Health;

public sealed record HealthStatusResponse(
    string Service,
    string Status,
    DateTimeOffset TimestampUtc);
