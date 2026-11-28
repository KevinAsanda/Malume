using Malume.Shared.Contracts.Escrow;

namespace Malume.Application.Abstractions.Jobs;

public interface IEscrowPayoutJob
{
    Task<EscrowPayoutRunResultDto> ProcessDueMilestonesAsync(
        DateOnly? asOfDateSast,
        CancellationToken cancellationToken);
}
