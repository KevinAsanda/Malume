using Malume.Api.Security;
using Malume.Application.Features.Escrow;
using Malume.Shared.Contracts.Auth;
using Malume.Shared.Contracts.Escrow;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Malume.Api.Controllers;

[ApiController]
[Route("api/v1/escrow")]
public sealed class EscrowController(ISender sender) : ControllerBase
{
    private readonly ISender _sender = sender;

    [HttpGet("subscriptions")]
    [Authorize(Roles = MalumeRoles.Parent + "," + MalumeRoles.Driver + "," + MalumeRoles.FleetOwner + "," + MalumeRoles.SysAdmin)]
    [ProducesResponseType<IReadOnlyList<SubscriptionEscrowDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SubscriptionEscrowDto>>> ListSubscriptionsAsync(
        CancellationToken cancellationToken)
    {
        IReadOnlyList<SubscriptionEscrowDto> subscriptions = await _sender.Send(
            new GetEscrowSubscriptionsQuery(),
            cancellationToken);
        return Ok(subscriptions);
    }

    [HttpGet("ledger")]
    [Authorize(Roles = MalumeRoles.Parent + "," + MalumeRoles.Driver + "," + MalumeRoles.FleetOwner + "," + MalumeRoles.SysAdmin)]
    [ProducesResponseType<WalletLedgerSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<WalletLedgerSummaryDto>> GetLedgerAsync(
        [FromQuery] Guid? walletAccountId = null,
        CancellationToken cancellationToken = default)
    {
        WalletLedgerSummaryDto ledger = await _sender.Send(
            new GetWalletLedgerQuery(walletAccountId),
            cancellationToken);
        return Ok(ledger);
    }

    [HttpPost("subscriptions/{subscriptionId:guid}/pro-rata-refund")]
    [Authorize(Policy = MalumePolicies.RequireSysAdmin)]
    [ProducesResponseType<ProRataRefundResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProRataRefundResponse>> IssueProRataRefundAsync(
        Guid subscriptionId,
        [FromBody] ProRataRefundRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        ProRataRefundResponse result = await _sender.Send(
            new ProcessProRataRefundCommand(subscriptionId, request.Reason),
            cancellationToken);
        return Ok(result);
    }
}
