using System.Net.Http.Json;
using System.Text.Json;
using Malume.Shared.Contracts.Compliance;
using Malume.Shared.Contracts.Drivers;
using Malume.Shared.Contracts.Endorsements;
using Malume.Shared.Contracts.Escrow;
using Malume.Shared.Contracts.Health;
using Malume.Shared.Contracts.Tenants;
using Malume.Shared.Contracts.Trips;
using Malume.Shared.Contracts.Vehicles;

namespace Malume.Web.Portal.Services;

public sealed class ApiClientException : Exception
{
    public ApiClientException()
    {
    }

    public ApiClientException(string message)
        : base(message)
    {
    }

    public ApiClientException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public ApiClientException(int statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}

public sealed class MalumeApiClient(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient;

    public Uri? BaseAddress => _httpClient.BaseAddress;

    public async Task<HealthStatusResponse?> GetApiHealthAsync(CancellationToken cancellationToken = default) =>
        await _httpClient.GetFromJsonAsync<HealthStatusResponse>("api/v1/health", cancellationToken);

    public async Task<HealthStatusResponse?> GetDatabaseHealthAsync(CancellationToken cancellationToken = default) =>
        await _httpClient.GetFromJsonAsync<HealthStatusResponse>("api/v1/health/database", cancellationToken);

    public async Task<FleetDto?> GetMyFleetOrNullAsync(CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response = await _httpClient.GetAsync("api/v1/fleets/mine", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<FleetDto>(cancellationToken: cancellationToken);
    }

    public async Task<FleetDto> CreateFleetAsync(CreateFleetRequest request, CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response = await _httpClient.PostAsJsonAsync("api/v1/fleets", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<FleetDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<IReadOnlyList<DriverProfileDto>> ListFleetDriversAsync(CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response = await _httpClient.GetAsync("api/v1/fleets/mine/drivers", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<IReadOnlyList<DriverProfileDto>>(cancellationToken: cancellationToken)) ?? [];
    }

    public async Task<DriverProfileDto> HireDriverAsync(HireDriverRequest request, CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response = await _httpClient.PostAsJsonAsync("api/v1/fleets/mine/drivers", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<DriverProfileDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<IReadOnlyList<VehicleDto>> ListMyVehiclesAsync(CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response = await _httpClient.GetAsync("api/v1/vehicles", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<IReadOnlyList<VehicleDto>>(cancellationToken: cancellationToken)) ?? [];
    }

    public async Task<VehicleDto> RegisterVehicleAsync(CreateVehicleRequest request, CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response = await _httpClient.PostAsJsonAsync("api/v1/vehicles", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<VehicleDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<VehicleDto> AssignVehicleDriverAsync(
        Guid vehicleId,
        Guid? driverProfileId,
        CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
            $"api/v1/vehicles/{vehicleId}/driver",
            new AssignVehicleDriverRequest(driverProfileId),
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<VehicleDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<SchoolDto?> GetMySchoolOrNullAsync(CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response = await _httpClient.GetAsync("api/v1/schools/mine", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<SchoolDto>(cancellationToken: cancellationToken);
    }

    public async Task<SchoolDto> CreateSchoolAsync(CreateSchoolRequest request, CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response = await _httpClient.PostAsJsonAsync("api/v1/schools", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<SchoolDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<IReadOnlyList<SchoolEndorsementDto>> ListMySchoolEndorsementsAsync(
        CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response = await _httpClient.GetAsync("api/v1/schools/mine/endorsements", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<IReadOnlyList<SchoolEndorsementDto>>(cancellationToken: cancellationToken)) ?? [];
    }

    public async Task<SchoolEndorsementDto> EndorseDriverAsync(
        EndorseDriverRequest request,
        CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
            "api/v1/schools/mine/endorsements",
            request,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<SchoolEndorsementDto>(cancellationToken: cancellationToken))!;
    }

    public async Task RevokeEndorsementAsync(Guid driverProfileId, CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response = await _httpClient.DeleteAsync(
            $"api/v1/schools/mine/endorsements/{driverProfileId}",
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<IReadOnlyList<DriverProfileDto>> ListActiveCompliantDriversAsync(
        CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response = await _httpClient.GetAsync(
            "api/v1/drivers?status=ActiveCompliant",
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<IReadOnlyList<DriverProfileDto>>(cancellationToken: cancellationToken)) ?? [];
    }

    public async Task<ComplianceReviewQueueDto> GetComplianceReviewQueueAsync(
        CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response = await _httpClient.GetAsync("api/v1/compliance/review-queue", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<ComplianceReviewQueueDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<ComplianceDocumentDto> VerifyComplianceDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response = await _httpClient.PostAsync(
            $"api/v1/compliance/documents/{documentId}/verify",
            null,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<ComplianceDocumentDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<DriverProfileDto> ApproveDriverComplianceAsync(
        Guid driverProfileId,
        CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response = await _httpClient.PostAsync(
            $"api/v1/compliance/drivers/{driverProfileId}/approve",
            null,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<DriverProfileDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<DriverProfileDto> SuspendDriverAsync(
        Guid driverProfileId,
        CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response = await _httpClient.PostAsync(
            $"api/v1/compliance/drivers/{driverProfileId}/suspend",
            null,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<DriverProfileDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<ComplianceScanResultDto> RunComplianceExpiryScanAsync(
        CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response = await _httpClient.PostAsync("api/v1/admin/jobs/compliance-scan", null, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<ComplianceScanResultDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<IReadOnlyList<SchoolDto>> ListSchoolsAsync(CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response = await _httpClient.GetAsync("api/v1/schools", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<IReadOnlyList<SchoolDto>>(cancellationToken: cancellationToken)) ?? [];
    }

    public async Task<SchoolGateBoardDto> GetSchoolGateBoardAsync(
        Guid schoolId,
        DateOnly? serviceDate = null,
        CancellationToken cancellationToken = default)
    {
        string path = serviceDate.HasValue
            ? $"api/v1/schools/{schoolId}/gate-board?serviceDate={serviceDate.Value:yyyy-MM-dd}"
            : $"api/v1/schools/{schoolId}/gate-board";
        HttpResponseMessage response = await _httpClient.GetAsync(path, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<SchoolGateBoardDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<IReadOnlyList<SubscriptionEscrowDto>> ListEscrowSubscriptionsAsync(
        CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response = await _httpClient.GetAsync("api/v1/escrow/subscriptions", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<IReadOnlyList<SubscriptionEscrowDto>>(cancellationToken: cancellationToken)) ?? [];
    }

    public async Task<WalletLedgerSummaryDto> GetWalletLedgerAsync(
        CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response = await _httpClient.GetAsync("api/v1/escrow/ledger", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<WalletLedgerSummaryDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<FleetDto> ConfigureFleetSubaccountAsync(
        ConfigureFleetSubaccountRequest request,
        CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
            "api/v1/payments/fleets/mine/subaccount",
            request,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<FleetDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<EscrowPayoutRunResultDto> RunEscrowPayoutsAsync(
        DateOnly? asOfDate = null,
        CancellationToken cancellationToken = default)
    {
        string path = asOfDate.HasValue
            ? $"api/v1/admin/jobs/escrow-payouts?asOfDate={asOfDate.Value:yyyy-MM-dd}"
            : "api/v1/admin/jobs/escrow-payouts";
        HttpResponseMessage response = await _httpClient.PostAsync(path, null, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<EscrowPayoutRunResultDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<ProRataRefundResponse> IssueProRataRefundAsync(
        Guid subscriptionId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
            $"api/v1/escrow/subscriptions/{subscriptionId}/pro-rata-refund",
            new ProRataRefundRequest(reason),
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<ProRataRefundResponse>(cancellationToken: cancellationToken))!;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string fallback = $"Request failed with status {(int)response.StatusCode} ({response.ReasonPhrase}).";
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(body);
                string? title = doc.RootElement.TryGetProperty("title", out JsonElement titleProp)
                    ? titleProp.GetString()
                    : null;
                string? detail = doc.RootElement.TryGetProperty("detail", out JsonElement detailProp)
                    ? detailProp.GetString()
                    : null;

                string combined = !string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(detail)
                    ? $"{title}: {detail}"
                    : detail ?? title ?? fallback;

                throw new ApiClientException((int)response.StatusCode, combined);
            }
            catch (JsonException)
            {
            }
        }

        throw new ApiClientException((int)response.StatusCode, fallback);
    }
}
