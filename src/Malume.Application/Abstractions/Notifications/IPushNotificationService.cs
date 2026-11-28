namespace Malume.Application.Abstractions.Notifications;

public sealed record PushNotificationMessage(
    string Title,
    string Body,
    IReadOnlyDictionary<string, string>? Data = null);

public interface IPushNotificationService
{
    Task<int> SendToUsersAsync(
        IReadOnlyCollection<Guid> userIds,
        PushNotificationMessage message,
        CancellationToken cancellationToken = default);
}

internal sealed class NullPushNotificationService : IPushNotificationService
{
    public Task<int> SendToUsersAsync(
        IReadOnlyCollection<Guid> userIds,
        PushNotificationMessage message,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(0);
}
