using Malume.Shared.Contracts.Compliance;

namespace Malume.Application.Abstractions.Jobs;

public interface IComplianceExpiryScanner
{
    Task<ComplianceScanResultDto> ScanAsync(CancellationToken cancellationToken);
}
