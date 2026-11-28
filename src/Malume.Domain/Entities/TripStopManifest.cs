using Malume.Domain.Common;
using Malume.Domain.Enums;
using Malume.Domain.Exceptions;
using Malume.Domain.ValueObjects;

namespace Malume.Domain.Entities;

public sealed class TripStopManifest : AuditableEntity
{
    private TripStopManifest()
    {
    }

    public TripStopManifest(
        Guid tripId,
        Guid learnerId,
        BookingType bookingType,
        int sequenceOrder,
        GeoCoordinate stopLocation,
        DateTimeOffset? estimatedArrivalUtc = null)
    {
        ArgumentNullException.ThrowIfNull(stopLocation);

        if (sequenceOrder <= 0)
        {
            throw new DomainException("Stop sequence order must be positive.");
        }

        TripId = tripId;
        LearnerId = learnerId;
        BookingType = bookingType;
        SequenceOrder = sequenceOrder;
        StopLocation = stopLocation;
        AttendanceStatus = AttendanceStatus.Scheduled;
        HandoverMethod = HandoverMethod.None;
        EstimatedArrivalUtc = estimatedArrivalUtc;
    }

    public Guid TripId { get; private set; }

    public Guid LearnerId { get; private set; }

    public BookingType BookingType { get; private set; }

    public int SequenceOrder { get; private set; }

    public GeoCoordinate StopLocation { get; private set; } = new(0.0, 0.0);

    public AttendanceStatus AttendanceStatus { get; private set; }

    public HandoverMethod HandoverMethod { get; private set; }

    public DateTimeOffset? HandoverTimestampUtc { get; private set; }

    public GeoCoordinate? HandoverGpsPoint { get; private set; }

    public DateTimeOffset? EstimatedArrivalUtc { get; private set; }

    public bool WasSyncedOffline { get; private set; }

    public Guid? OfflineEventId { get; private set; }

    public void MarkAbsent()
    {
        if (AttendanceStatus is AttendanceStatus.PickedUp or AttendanceStatus.DroppedOff)
        {
            throw new DomainException("Cannot mark learner absent after pickup or drop-off has already occurred.");
        }

        AttendanceStatus = AttendanceStatus.MarkedAbsent;
        MarkUpdated();
    }

    public void RecordHandover(
        AttendanceStatus attendanceStatus,
        HandoverMethod handoverMethod,
        DateTimeOffset timestampUtc,
        GeoCoordinate gpsPoint,
        bool wasSyncedOffline = false,
        Guid? offlineEventId = null)
    {
        if (attendanceStatus is not (AttendanceStatus.PickedUp or AttendanceStatus.DroppedOff))
        {
            throw new DomainException("Handover attendance status must be PickedUp or DroppedOff.");
        }

        if (handoverMethod == HandoverMethod.None)
        {
            throw new DomainException("Handover method must be QrScan or FallbackPin.");
        }

        ArgumentNullException.ThrowIfNull(gpsPoint);

        AttendanceStatus = attendanceStatus;
        HandoverMethod = handoverMethod;
        HandoverTimestampUtc = timestampUtc;
        HandoverGpsPoint = gpsPoint;
        WasSyncedOffline = wasSyncedOffline;
        OfflineEventId = offlineEventId;
        MarkUpdated();
    }

    public void UpdateEstimatedArrival(DateTimeOffset newEtaUtc)
    {
        EstimatedArrivalUtc = newEtaUtc;
        MarkUpdated();
    }
}
