using Malume.Domain.ValueObjects;

namespace Malume.Application.Abstractions.Payments;

public sealed record PaystackInitializeResult(
    string Reference,
    string AuthorizationUrl,
    string AccessCode);

public sealed record PaystackVerifyResult(
    bool Verified,
    string Reference,
    decimal AmountZar,
    string? GatewayResponse);

public sealed record PaystackSubaccountResult(
    string SubaccountCode,
    string BusinessName);

public sealed record PaystackTransferResult(
    string TransferCode,
    string Reference);

public sealed record PaystackRefundResult(
    string RefundReference,
    decimal AmountZar);

public interface IPaystackGateway
{
    string PublicKey { get; }

    Task<PaystackInitializeResult> InitializeTransactionAsync(
        string email,
        ZarMoney amount,
        string reference,
        string? subaccountCode,
        string? callbackUrl,
        CancellationToken cancellationToken);

    Task<PaystackVerifyResult> VerifyTransactionAsync(
        string reference,
        CancellationToken cancellationToken);

    Task<PaystackSubaccountResult> CreateSubaccountAsync(
        string businessName,
        string settlementBankCode,
        string accountNumber,
        decimal percentageCharge,
        CancellationToken cancellationToken);

    Task<PaystackTransferResult> InitiateMilestonePayoutAsync(
        string recipientCode,
        ZarMoney amount,
        string reason,
        string reference,
        CancellationToken cancellationToken);

    Task<PaystackRefundResult> InitiateRefundAsync(
        string transactionReference,
        ZarMoney amount,
        string reason,
        CancellationToken cancellationToken);

    bool VerifyWebhookSignature(string rawRequestBody, string signatureHeader);
}
