namespace Malume.Shared.Contracts.Notifications;

public sealed record RegisterDeviceTokenRequest(
    string Token,
    string Platform = "Android",
    string AppVariant = "Parent");

public sealed record DeviceTokenRegistrationDto(
    Guid DeviceTokenId,
    Guid UserId,
    string Platform,
    string AppVariant,
    DateTimeOffset LastSeenAtUtc);
