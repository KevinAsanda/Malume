using Malume.Domain.Common;
using Malume.Domain.Enums;
using Malume.Domain.ValueObjects;

namespace Malume.Domain.Entities;

public sealed class WalletLedgerEntry : Entity
{
    private WalletLedgerEntry()
    {
    }

    public WalletLedgerEntry(
        Guid transactionGroupId,
        Guid walletAccountId,
        LedgerEntryType entryType,
        ZarMoney amount,
        LedgerReferenceType referenceType,
        string? paystackReference = null,
        string? notes = null)
    {
        ArgumentNullException.ThrowIfNull(amount);

        TransactionGroupId = transactionGroupId;
        WalletAccountId = walletAccountId;
        EntryType = entryType;
        Amount = amount;
        ReferenceType = referenceType;
        PaystackReference = paystackReference?.Trim();
        Notes = notes?.Trim();
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid TransactionGroupId { get; private set; }

    public Guid WalletAccountId { get; private set; }

    public LedgerEntryType EntryType { get; private set; }

    public ZarMoney Amount { get; private set; } = ZarMoney.Zero;

    public LedgerReferenceType ReferenceType { get; private set; }

    public string? PaystackReference { get; private set; }

    public string? Notes { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
}
