using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Malume.Domain.Enums;
using Malume.Domain.Exceptions;

namespace Malume.Domain.ValueObjects;

public static class DailyRotatingPin
{
    public static string Derive(
        ReadOnlySpan<byte> learnerQrSecretSalt,
        Guid learnerId,
        DateOnly serviceDate,
        RouteDirection direction)
    {
        if (learnerQrSecretSalt.Length < 16)
        {
            throw new DomainException("Learner QR secret salt must be at least 16 bytes.");
        }

        string inputMessage = string.Create(
            CultureInfo.InvariantCulture,
            $"{learnerId:D}:{serviceDate:yyyyMMdd}:{(int)direction}");

        byte[] messageBytes = Encoding.UTF8.GetBytes(inputMessage);
        Span<byte> digest = stackalloc byte[32];
        HMACSHA256.HashData(learnerQrSecretSalt, messageBytes, digest);

        int offset = digest[^1] & 0x0F;
        int binaryCode = ((digest[offset] & 0x7F) << 24)
            | ((digest[offset + 1] & 0xFF) << 16)
            | ((digest[offset + 2] & 0xFF) << 8)
            | (digest[offset + 3] & 0xFF);

        int fourDigitPin = (binaryCode % 9000) + 1000;
        return fourDigitPin.ToString("D4", CultureInfo.InvariantCulture);
    }

    public static string ComputeVerifierHash(
        ReadOnlySpan<byte> learnerQrSecretSalt,
        Guid learnerId,
        DateOnly serviceDate,
        RouteDirection direction)
    {
        string pin = Derive(learnerQrSecretSalt, learnerId, serviceDate, direction);
        string payload = string.Create(
            CultureInfo.InvariantCulture,
            $"{learnerId:D}:{serviceDate:yyyyMMdd}:{(int)direction}:{pin}");

        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(payload), hash);
        return Convert.ToHexString(hash);
    }

    public static bool VerifyPin(
        ReadOnlySpan<byte> learnerQrSecretSalt,
        Guid learnerId,
        DateOnly serviceDate,
        RouteDirection direction,
        string candidatePin)
    {
        if (string.IsNullOrWhiteSpace(candidatePin) || candidatePin.Length != 4)
        {
            return false;
        }

        string expectedPin = Derive(learnerQrSecretSalt, learnerId, serviceDate, direction);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedPin),
            Encoding.UTF8.GetBytes(candidatePin));
    }
}
