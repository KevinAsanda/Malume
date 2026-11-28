using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Malume.Application.Abstractions.Payments;
using Malume.Domain.Exceptions;
using Malume.Domain.ValueObjects;
using Microsoft.Extensions.Options;

namespace Malume.Infrastructure.Payments;

internal sealed class PaystackHttpGateway(
    HttpClient httpClient,
    IOptions<PaystackOptions> options) : IPaystackGateway
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly PaystackOptions _options = options.Value;

    public string PublicKey => _options.PublicKey ?? string.Empty;

    private bool HasLiveSecretKey => !string.IsNullOrWhiteSpace(_options.SecretKey);

    public async Task<PaystackInitializeResult> InitializeTransactionAsync(
        string email,
        ZarMoney amount,
        string reference,
        string? subaccountCode,
        string? callbackUrl,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentNullException.ThrowIfNull(amount);
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);

        if (!HasLiveSecretKey)
        {
            return new PaystackInitializeResult(
                reference,
                $"https://checkout.paystack.com/sim/{reference}",
                $"ACCESS_{reference}");
        }

        long amountMinorUnits = (long)decimal.Round(amount.Amount * 100m, 0, MidpointRounding.AwayFromZero);
        Dictionary<string, object> payload = new()
        {
            ["email"] = email,
            ["amount"] = amountMinorUnits.ToString(CultureInfo.InvariantCulture),
            ["currency"] = "ZAR",
            ["reference"] = reference
        };

        if (!string.IsNullOrWhiteSpace(subaccountCode))
        {
            payload["subaccount"] = subaccountCode;
        }

        if (!string.IsNullOrWhiteSpace(callbackUrl))
        {
            payload["callback_url"] = callbackUrl;
        }

        using var request = CreateAuthorizedRequest(HttpMethod.Post, "transaction/initialize");
        request.Content = JsonContent.Create(payload);
        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        using JsonDocument doc = await ReadPaystackJsonAsync(response, cancellationToken);

        JsonElement data = doc.RootElement.GetProperty("data");
        return new PaystackInitializeResult(
            data.GetProperty("reference").GetString() ?? reference,
            data.GetProperty("authorization_url").GetString() ?? string.Empty,
            data.GetProperty("access_code").GetString() ?? string.Empty);
    }

    public async Task<PaystackVerifyResult> VerifyTransactionAsync(
        string reference,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);

        if (!HasLiveSecretKey)
        {
            return new PaystackVerifyResult(true, reference, 0m, "Simulated Paystack Test Verification");
        }

        using var request = CreateAuthorizedRequest(
            HttpMethod.Get,
            $"transaction/verify/{Uri.EscapeDataString(reference)}");
        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        using JsonDocument doc = await ReadPaystackJsonAsync(response, cancellationToken);

        JsonElement data = doc.RootElement.GetProperty("data");
        string? txStatus = data.TryGetProperty("status", out JsonElement statusEl) ? statusEl.GetString() : null;
        long amountMinor = data.TryGetProperty("amount", out JsonElement amountEl) && amountEl.TryGetInt64(out long minor)
            ? minor
            : 0L;
        string? gatewayResponse = data.TryGetProperty("gateway_response", out JsonElement respEl)
            ? respEl.GetString()
            : null;

        return new PaystackVerifyResult(
            string.Equals(txStatus, "success", StringComparison.OrdinalIgnoreCase),
            reference,
            decimal.Round(amountMinor / 100m, 2, MidpointRounding.AwayFromZero),
            gatewayResponse);
    }

    public async Task<PaystackSubaccountResult> CreateSubaccountAsync(
        string businessName,
        string settlementBankCode,
        string accountNumber,
        decimal percentageCharge,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(businessName);
        ArgumentException.ThrowIfNullOrWhiteSpace(settlementBankCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountNumber);

        if (!HasLiveSecretKey)
        {
            string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            return new PaystackSubaccountResult($"ACCT_SIM_{suffix}", businessName);
        }

        var payload = new
        {
            business_name = businessName,
            settlement_bank = settlementBankCode,
            account_number = accountNumber,
            percentage_charge = percentageCharge
        };

        using var request = CreateAuthorizedRequest(HttpMethod.Post, "subaccount");
        request.Content = JsonContent.Create(payload);
        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        using JsonDocument doc = await ReadPaystackJsonAsync(response, cancellationToken);

        JsonElement data = doc.RootElement.GetProperty("data");
        string code = data.GetProperty("subaccount_code").GetString()
            ?? throw new DomainException("Paystack subaccount response omitted subaccount_code.");
        return new PaystackSubaccountResult(code, businessName);
    }

    public async Task<PaystackTransferResult> InitiateMilestonePayoutAsync(
        string recipientCode,
        ZarMoney amount,
        string reason,
        string reference,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientCode);
        ArgumentNullException.ThrowIfNull(amount);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);

        if (!HasLiveSecretKey || !recipientCode.StartsWith("RCP_", StringComparison.OrdinalIgnoreCase))
        {
            return new PaystackTransferResult($"TRF_TEST_{reference}", reference);
        }

        long amountMinorUnits = (long)decimal.Round(amount.Amount * 100m, 0, MidpointRounding.AwayFromZero);
        var payload = new
        {
            source = "balance",
            amount = amountMinorUnits,
            recipient = recipientCode,
            currency = "ZAR",
            reason,
            reference
        };

        using var request = CreateAuthorizedRequest(HttpMethod.Post, "transfer");
        request.Content = JsonContent.Create(payload);
        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        using JsonDocument doc = await ReadPaystackJsonAsync(response, cancellationToken);

        JsonElement data = doc.RootElement.GetProperty("data");
        string transferCode = data.GetProperty("transfer_code").GetString() ?? $"TRF_{reference}";
        return new PaystackTransferResult(transferCode, reference);
    }

    public async Task<PaystackRefundResult> InitiateRefundAsync(
        string transactionReference,
        ZarMoney amount,
        string reason,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transactionReference);
        ArgumentNullException.ThrowIfNull(amount);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (!HasLiveSecretKey || transactionReference.StartsWith("MLM-TEST", StringComparison.OrdinalIgnoreCase))
        {
            return new PaystackRefundResult($"RFND_TEST_{Guid.NewGuid():N}"[..20], amount.Amount);
        }

        long amountMinorUnits = (long)decimal.Round(amount.Amount * 100m, 0, MidpointRounding.AwayFromZero);
        var payload = new
        {
            transaction = transactionReference,
            amount = amountMinorUnits,
            currency = "ZAR",
            merchant_note = reason
        };

        using var request = CreateAuthorizedRequest(HttpMethod.Post, "refund");
        request.Content = JsonContent.Create(payload);
        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        using JsonDocument doc = await ReadPaystackJsonAsync(response, cancellationToken);

        JsonElement data = doc.RootElement.GetProperty("data");
        string refundRef = data.TryGetProperty("id", out JsonElement idEl)
            ? $"RFND_{idEl}"
            : $"RFND_{transactionReference}";
        return new PaystackRefundResult(refundRef, amount.Amount);
    }

    public bool VerifyWebhookSignature(string rawRequestBody, string signatureHeader)
    {
        string? secret = !string.IsNullOrWhiteSpace(_options.WebhookSecret)
            ? _options.WebhookSecret
            : _options.SecretKey;

        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(signatureHeader))
        {
            return false;
        }

        byte[] keyBytes = Encoding.UTF8.GetBytes(secret.Trim());
        byte[] bodyBytes = Encoding.UTF8.GetBytes(rawRequestBody ?? string.Empty);
        byte[] hash = HMACSHA512.HashData(keyBytes, bodyBytes);
        string computedHex = Convert.ToHexString(hash).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHex),
            Encoding.UTF8.GetBytes(signatureHeader.Trim().ToLowerInvariant()));
    }

    private HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string relativePath)
    {
        string baseUrl = _options.BaseUrl.TrimEnd('/') + "/";
        var request = new HttpRequestMessage(method, new Uri(new Uri(baseUrl), relativePath));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.SecretKey!.Trim());
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private static async Task<JsonDocument> ReadPaystackJsonAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new DomainException($"Paystack API error ({(int)response.StatusCode}): {body}");
        }

        return JsonDocument.Parse(body);
    }
}
