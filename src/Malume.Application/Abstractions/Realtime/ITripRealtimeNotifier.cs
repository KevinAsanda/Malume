using Malume.Shared.Contracts.Trips;

namespace Malume.Application.Abstractions.Realtime;

public interface ITripRealtimeNotifier
{
    Task NotifyTripStatusChangedAsync(
        Guid tripId,
        string status,
        DateTimeOffset changedAtUtc,
        CancellationToken cancellationToken);

    Task NotifyHandoverRecordedAsync(
        Guid tripId,
        Guid learnerId,
        string attendanceStatus,
        string handoverMethod,
        DateTimeOffset timestampUtc,
        CancellationToken cancellationToken);

    Task NotifySosTriggeredAsync(
        SosAlertDto alert,
        CancellationToken cancellationToken);
}

internal sealed class NullTripRealtimeNotifier : ITripRealtimeNotifier
{
    public Task NotifyTripStatusChangedAsync(
        Guid tripId,
        string status,
        DateTimeOffset changedAtUtc,
        CancellationToken cancellationToken) => Task.CompletedTask;

    public Task NotifyHandoverRecordedAsync(
        Guid tripId,
        Guid learnerId,
        string attendanceStatus,
        string handoverMethod,
        DateTimeOffset timestampUtc,
        CancellationToken cancellationToken) => Task.CompletedTask;

    public Task NotifySosTriggeredAsync(
        SosAlertDto alert,
        CancellationToken cancellationToken) => Task.CompletedTask;
}
