namespace Malume.Shared.Contracts.Escrow;

public sealed record InitializeSubscriptionPaymentRequest(
    Guid RouteId,
    Guid LearnerId,
    DateOnly? BillingMonthStart = null,
    string? CallbackUrl = null);

public sealed record InitializeSubscriptionPaymentResponse(
    Guid SubscriptionId,
    string PaystackReference,
    string AuthorizationUrl,
    string AccessCode,
    string PublicKey,
    decimal TotalAmountZar);

public sealed record VerifySubscriptionPaymentResponse(
    Guid SubscriptionId,
    string PaystackReference,
    string EscrowStatus,
    bool PaymentVerified,
    decimal TotalAmountZar);

public sealed record ConfigureFleetSubaccountRequest(
    string BusinessName,
    string SettlementBankCode,
    string AccountNumber,
    decimal PercentageCharge = 5.0m);

public sealed record EscrowMilestoneDto(
    Guid MilestoneId,
    int TrancheNumber,
    decimal Percentage,
    decimal AmountZar,
    string Status,
    DateOnly ScheduledReleaseDate,
    DateTimeOffset? ReleasedAtUtc,
    string? PaystackTransferCode);

public sealed record SubscriptionEscrowDto(
    Guid SubscriptionId,
    Guid RouteId,
    string RouteName,
    Guid LearnerId,
    string LearnerFullName,
    string BillingYearMonth,
    decimal TotalAmountZar,
    string EscrowStatus,
    string? PaystackReference,
    IReadOnlyList<EscrowMilestoneDto> Milestones);

public sealed record WalletLedgerEntryDto(
    Guid LedgerEntryId,
    Guid TransactionGroupId,
    Guid WalletAccountId,
    string EntryType,
    decimal AmountZar,
    string ReferenceType,
    string? PaystackReference,
    string? Notes,
    DateTimeOffset CreatedAtUtc);

public sealed record WalletLedgerSummaryDto(
    Guid WalletAccountId,
    decimal TotalCreditsZar,
    decimal TotalDebitsZar,
    decimal NetBalanceZar,
    IReadOnlyList<WalletLedgerEntryDto> Entries);

public sealed record ProRataRefundRequest(string Reason);

public sealed record ProRataRefundResponse(
    Guid SubscriptionId,
    decimal RefundedAmountZar,
    int RefundedMilestoneCount,
    string RefundReference);

public sealed record EscrowPayoutRunResultDto(
    DateOnly EvaluatedDateSast,
    int SubscriptionsChecked,
    int MilestonesReleased,
    int SubscriptionsPausedForCompliance,
    int TranchesSkippedBelowRatio,
    decimal TotalReleasedZar);
