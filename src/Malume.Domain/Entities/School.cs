using Malume.Domain.Common;
using Malume.Domain.Exceptions;
using Malume.Domain.ValueObjects;

namespace Malume.Domain.Entities;

public sealed class School : AuditableEntity
{
    public const int DefaultGateGeofenceRadiusMeters = 150;

    private School()
    {
    }

    public School(
        string name,
        GeoCoordinate schoolGateLocation,
        string addressText,
        string? emisNumber = null,
        int gateGeofenceRadiusMeters = DefaultGateGeofenceRadiusMeters,
        Guid? adminUserId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(schoolGateLocation);
        ArgumentException.ThrowIfNullOrWhiteSpace(addressText);

        if (gateGeofenceRadiusMeters <= 0)
        {
            throw new DomainException("School gate geofence radius must be greater than zero.");
        }

        Name = name.Trim();
        SchoolGateLocation = schoolGateLocation;
        AddressText = addressText.Trim();
        EmisNumber = emisNumber?.Trim();
        GateGeofenceRadiusMeters = gateGeofenceRadiusMeters;
        AdminUserId = adminUserId;
    }

    public string Name { get; private set; } = string.Empty;

    public string? EmisNumber { get; private set; }

    public string AddressText { get; private set; } = string.Empty;

    public GeoCoordinate SchoolGateLocation { get; private set; } = new(0.0, 0.0);

    public int GateGeofenceRadiusMeters { get; private set; } = DefaultGateGeofenceRadiusMeters;

    public Guid? AdminUserId { get; private set; }
}
