using Malume.Domain.Exceptions;
using Malume.Domain.ValueObjects;
using Shouldly;

namespace Malume.Domain.UnitTests.ValueObjects;

public sealed class ZarMoneyTests
{
    [Fact]
    public void Constructor_WhenNegative_ShouldThrowDomainException()
    {
        Should.Throw<DomainException>(() => new ZarMoney(-0.01m));
    }

    [Fact]
    public void Percentage_ShouldRoundAwayFromZeroToTwoDecimals()
    {
        ZarMoney amount = ZarMoney.Of(1200m);

        amount.Percentage(40m).Amount.ShouldBe(480.00m);
        amount.Percentage(30m).Amount.ShouldBe(360.00m);
    }

    [Fact]
    public void Subtraction_WhenResultNegative_ShouldThrowDomainException()
    {
        ZarMoney smaller = ZarMoney.Of(50m);
        ZarMoney larger = ZarMoney.Of(100m);

        Should.Throw<DomainException>(() => _ = smaller - larger);
    }
}
