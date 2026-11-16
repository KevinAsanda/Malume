using System.Security.Cryptography;
using Malume.Domain.Enums;
using Malume.Domain.Exceptions;
using Malume.Domain.ValueObjects;
using Shouldly;

namespace Malume.Domain.UnitTests.ValueObjects;

public sealed class DailyRotatingPinTests
{
    [Fact]
    public void Derive_ShouldBeDeterministicFourDigitsWithinRange()
    {
        byte[] salt = RandomNumberGenerator.GetBytes(32);
        Guid learnerId = Guid.NewGuid();
        var serviceDate = new DateOnly(2026, 11, 16);

        string pin1 = DailyRotatingPin.Derive(salt, learnerId, serviceDate, RouteDirection.MorningToSchool);
        string pin2 = DailyRotatingPin.Derive(salt, learnerId, serviceDate, RouteDirection.MorningToSchool);

        pin1.ShouldBe(pin2);
        pin1.Length.ShouldBe(4);
        int numericPin = int.Parse(pin1, System.Globalization.CultureInfo.InvariantCulture);
        numericPin.ShouldBeInRange(1000, 9999);
    }

    [Fact]
    public void VerifyPin_ShouldReturnTrueForExpectedPinAndFalseForWrongPin()
    {
        byte[] salt = RandomNumberGenerator.GetBytes(32);
        Guid learnerId = Guid.NewGuid();
        var serviceDate = new DateOnly(2026, 11, 16);

        string pin = DailyRotatingPin.Derive(salt, learnerId, serviceDate, RouteDirection.AfternoonFromSchool);

        DailyRotatingPin.VerifyPin(salt, learnerId, serviceDate, RouteDirection.AfternoonFromSchool, pin).ShouldBeTrue();
        DailyRotatingPin.VerifyPin(salt, learnerId, serviceDate, RouteDirection.AfternoonFromSchool, "0000").ShouldBeFalse();
        DailyRotatingPin.VerifyPin(salt, learnerId, serviceDate.AddDays(1), RouteDirection.AfternoonFromSchool, pin).ShouldBeFalse();
    }

    [Fact]
    public void Derive_WhenSaltTooShort_ShouldThrowDomainException()
    {
        Should.Throw<DomainException>(() =>
            DailyRotatingPin.Derive(new byte[8], Guid.NewGuid(), new DateOnly(2026, 11, 16), RouteDirection.MorningToSchool));
    }
}
