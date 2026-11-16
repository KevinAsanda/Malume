using System.Security.Cryptography;
using Malume.Domain.Common;
using Malume.Domain.Enums;
using Malume.Domain.Exceptions;
using Malume.Domain.ValueObjects;

namespace Malume.Domain.Entities;

public sealed class Learner : AuditableEntity
{
    private byte[] _qrSecretSalt = [];

    private Learner()
    {
    }

    public Learner(
        Guid parentUserId,
        Guid schoolId,
        string fullName,
        string grade,
        GeoCoordinate homePickupPoint,
        string homeAddressText,
        string emergencyContactPhone,
        byte[]? qrSecretSalt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(grade);
        ArgumentNullException.ThrowIfNull(homePickupPoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(homeAddressText);
        ArgumentException.ThrowIfNullOrWhiteSpace(emergencyContactPhone);

        byte[] salt = qrSecretSalt ?? RandomNumberGenerator.GetBytes(32);
        if (salt.Length < 16)
        {
            throw new DomainException("Learner QR secret salt must be at least 16 bytes.");
        }

        ParentUserId = parentUserId;
        SchoolId = schoolId;
        FullName = fullName.Trim();
        Grade = grade.Trim();
        HomePickupPoint = homePickupPoint;
        HomeAddressText = homeAddressText.Trim();
        EmergencyContactPhone = emergencyContactPhone.Trim();
        _qrSecretSalt = salt.ToArray();
    }

    public Guid ParentUserId { get; private set; }

    public Guid SchoolId { get; private set; }

    public string FullName { get; private set; } = string.Empty;

    public string Grade { get; private set; } = string.Empty;

    public GeoCoordinate HomePickupPoint { get; private set; } = new(0.0, 0.0);

    public string HomeAddressText { get; private set; } = string.Empty;

    public string EmergencyContactPhone { get; private set; } = string.Empty;

    public ReadOnlySpan<byte> GetQrSecretSalt() => _qrSecretSalt;

    public string DeriveDailyPin(DateOnly serviceDate, RouteDirection direction) =>
        DailyRotatingPin.Derive(_qrSecretSalt, Id, serviceDate, direction);

    public string ComputeDailyPinVerifierHash(DateOnly serviceDate, RouteDirection direction) =>
        DailyRotatingPin.ComputeVerifierHash(_qrSecretSalt, Id, serviceDate, direction);

    public bool VerifyDailyPin(DateOnly serviceDate, RouteDirection direction, string candidatePin) =>
        DailyRotatingPin.VerifyPin(_qrSecretSalt, Id, serviceDate, direction, candidatePin);
}
