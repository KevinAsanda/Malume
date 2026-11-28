using Malume.Domain.Entities;
using Malume.Domain.Enums;
using Malume.Domain.Exceptions;
using Malume.Domain.ValueObjects;

namespace Malume.Domain.Services;

public static class EscrowAccountingService
{
    public static readonly Guid PlatformEscrowLiabilityAccountId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static readonly Guid PaystackSettlementClearingAccountId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static IReadOnlyList<WalletLedgerEntry> CreateSubscriptionChargeEntries(
        ZarMoney totalAmount,
        string paystackReference,
        string? notes = null)
    {
        ArgumentNullException.ThrowIfNull(totalAmount);
        ArgumentException.ThrowIfNullOrWhiteSpace(paystackReference);

        Guid txGroupId = Guid.NewGuid();
        return
        [
            new WalletLedgerEntry(
                txGroupId,
                PaystackSettlementClearingAccountId,
                LedgerEntryType.Debit,
                totalAmount,
                LedgerReferenceType.SubscriptionCharge,
                paystackReference,
                notes ?? "Paystack subscription charge clearing debit"),
            new WalletLedgerEntry(
                txGroupId,
                PlatformEscrowLiabilityAccountId,
                LedgerEntryType.Credit,
                totalAmount,
                LedgerReferenceType.SubscriptionCharge,
                paystackReference,
                notes ?? "Parent escrow liability credit")
        ];
    }

    public static IReadOnlyList<WalletLedgerEntry> CreateMilestoneReleaseEntries(
        Guid beneficiaryWalletAccountId,
        int trancheNumber,
        ZarMoney amount,
        string paystackTransferCode,
        string? notes = null)
    {
        ArgumentNullException.ThrowIfNull(amount);
        ArgumentException.ThrowIfNullOrWhiteSpace(paystackTransferCode);

        if (beneficiaryWalletAccountId == Guid.Empty)
        {
            throw new DomainException("Beneficiary wallet account ID cannot be empty.");
        }

        LedgerReferenceType referenceType = trancheNumber switch
        {
            1 => LedgerReferenceType.MilestoneRelease40,
            2 => LedgerReferenceType.MilestoneRelease30Mid,
            3 => LedgerReferenceType.MilestoneRelease30End,
            _ => throw new DomainException($"Unsupported milestone tranche number '{trancheNumber}'.")
        };

        Guid txGroupId = Guid.NewGuid();
        return
        [
            new WalletLedgerEntry(
                txGroupId,
                PlatformEscrowLiabilityAccountId,
                LedgerEntryType.Debit,
                amount,
                referenceType,
                paystackTransferCode,
                notes ?? $"Escrow tranche {trancheNumber} release debit"),
            new WalletLedgerEntry(
                txGroupId,
                beneficiaryWalletAccountId,
                LedgerEntryType.Credit,
                amount,
                referenceType,
                paystackTransferCode,
                notes ?? $"Beneficiary tranche {trancheNumber} payout credit")
        ];
    }

    public static IReadOnlyList<WalletLedgerEntry> CreateProRataRefundEntries(
        Guid parentUserId,
        ZarMoney amount,
        string refundReference,
        string? notes = null)
    {
        ArgumentNullException.ThrowIfNull(amount);
        ArgumentException.ThrowIfNullOrWhiteSpace(refundReference);

        if (parentUserId == Guid.Empty)
        {
            throw new DomainException("Parent wallet account ID cannot be empty.");
        }

        Guid txGroupId = Guid.NewGuid();
        return
        [
            new WalletLedgerEntry(
                txGroupId,
                PlatformEscrowLiabilityAccountId,
                LedgerEntryType.Debit,
                amount,
                LedgerReferenceType.ProRataRefund,
                refundReference,
                notes ?? "Escrow pro-rata refund liability debit"),
            new WalletLedgerEntry(
                txGroupId,
                parentUserId,
                LedgerEntryType.Credit,
                amount,
                LedgerReferenceType.ProRataRefund,
                refundReference,
                notes ?? "Parent pro-rata refund credit")
        ];
    }
}
