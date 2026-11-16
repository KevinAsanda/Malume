using Malume.Domain.Exceptions;

namespace Malume.Domain.ValueObjects;

public sealed record ZarMoney
{
    public const string DefaultCurrency = "ZAR";

    public decimal Amount { get; }

    public ZarMoney(decimal amount)
    {
        if (amount < 0m)
        {
            throw new DomainException("ZAR monetary amount cannot be negative.");
        }

        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
    }

    public static ZarMoney Zero => new(0m);

    public static ZarMoney Of(decimal amount) => new(amount);

    public ZarMoney Percentage(decimal percent)
    {
        if (percent is < 0m or > 100m)
        {
            throw new DomainException("Percentage must be between 0 and 100.");
        }

        return new ZarMoney(decimal.Round(Amount * (percent / 100m), 2, MidpointRounding.AwayFromZero));
    }

    public ZarMoney Add(ZarMoney other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new ZarMoney(Amount + other.Amount);
    }

    public ZarMoney Subtract(ZarMoney other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (other.Amount > Amount)
        {
            throw new DomainException("Resulting ZAR monetary amount cannot be negative.");
        }

        return new ZarMoney(Amount - other.Amount);
    }

    public static ZarMoney operator +(ZarMoney left, ZarMoney right)
    {
        ArgumentNullException.ThrowIfNull(left);
        return left.Add(right);
    }

    public static ZarMoney operator -(ZarMoney left, ZarMoney right)
    {
        ArgumentNullException.ThrowIfNull(left);
        return left.Subtract(right);
    }
}
