using Malume.Domain.Common;
using Malume.Domain.Exceptions;

namespace Malume.Domain.Entities;

public sealed class UserDeviceToken : AuditableEntity
{
    private UserDeviceToken()
    {
    }

    public UserDeviceToken(
        Guid userId,
        string token,
        string platform,
        string appVariant,
        DateTimeOffset seenAtUtc)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("Device token user ID cannot be empty.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        ArgumentException.ThrowIfNullOrWhiteSpace(platform);
        ArgumentException.ThrowIfNullOrWhiteSpace(appVariant);

        UserId = userId;
        Token = token.Trim();
        Platform = platform.Trim();
        AppVariant = appVariant.Trim();
        LastSeenAtUtc = seenAtUtc;
    }

    public Guid UserId { get; private set; }

    public string Token { get; private set; } = string.Empty;

    public string Platform { get; private set; } = string.Empty;

    public string AppVariant { get; private set; } = string.Empty;

    public DateTimeOffset LastSeenAtUtc { get; private set; }

    public void RefreshSeen(string platform, string appVariant, DateTimeOffset seenAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platform);
        ArgumentException.ThrowIfNullOrWhiteSpace(appVariant);

        Platform = platform.Trim();
        AppVariant = appVariant.Trim();
        LastSeenAtUtc = seenAtUtc;
        MarkUpdated();
    }
}
