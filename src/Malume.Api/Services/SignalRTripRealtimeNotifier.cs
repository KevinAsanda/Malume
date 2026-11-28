using Malume.Api.Hubs;
using Malume.Application.Abstractions.Realtime;
using Malume.Shared.Contracts.Trips;
using Microsoft.AspNetCore.SignalR;

namespace Malume.Api.Services;

internal sealed class SignalRTripRealtimeNotifier(IHubContext<TripTelemetryHub> hubContext) : ITripRealtimeNotifier
{
    private readonly IHubContext<TripTelemetryHub> _hubContext = hubContext;

    public Task NotifyTripStatusChangedAsync(
        Guid tripId,
        string status,
        DateTimeOffset changedAtUtc,
        CancellationToken cancellationToken) =>
        _hubContext.Clients.Group(TripTelemetryHub.TripGroup(tripId)).SendAsync(
            "TripStatusChanged",
            new { TripId = tripId, Status = status, ChangedAtUtc = changedAtUtc },
            cancellationToken);

    public Task NotifyHandoverRecordedAsync(
        Guid tripId,
        Guid learnerId,
        string attendanceStatus,
        string handoverMethod,
        DateTimeOffset timestampUtc,
        CancellationToken cancellationToken) =>
        _hubContext.Clients.Group(TripTelemetryHub.TripGroup(tripId)).SendAsync(
            "HandoverRecorded",
            new
            {
                TripId = tripId,
                LearnerId = learnerId,
                AttendanceStatus = attendanceStatus,
                HandoverMethod = handoverMethod,
                TimestampUtc = timestampUtc
            },
            cancellationToken);

    public async Task NotifySosTriggeredAsync(
        SosAlertDto alert,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(alert);

        await _hubContext.Clients.Group(TripTelemetryHub.TripGroup(alert.TripId)).SendAsync(
            "SosIncidentCreated",
            alert,
            cancellationToken);

        await _hubContext.Clients.Group(TripTelemetryHub.AdminSosGroup).SendAsync(
            "SosIncidentCreated",
            alert,
            cancellationToken);
    }
}
