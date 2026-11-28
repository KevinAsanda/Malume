using Malume.Api.Security;
using Malume.Application.Features.Compliance;
using Malume.Application.Features.Escrow;
using Malume.Shared.Contracts.Compliance;
using Malume.Shared.Contracts.Escrow;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Malume.Api.Controllers;

[ApiController]
[Route("api/v1/admin/jobs")]
[Authorize(Policy = MalumePolicies.RequireSysAdmin)]
public sealed class AdminJobsController(ISender sender) : ControllerBase
{
    private readonly ISender _sender = sender;

    [HttpPost("compliance-scan")]
    [ProducesResponseType<ComplianceScanResultDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ComplianceScanResultDto>> RunComplianceScanAsync(
        CancellationToken cancellationToken)
    {
        ComplianceScanResultDto result = await _sender.Send(
            new RunComplianceExpiryScanCommand(),
            cancellationToken);
        return Ok(result);
    }

    [HttpPost("escrow-payouts")]
    [ProducesResponseType<EscrowPayoutRunResultDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<EscrowPayoutRunResultDto>> RunEscrowPayoutsAsync(
        [FromQuery] DateOnly? asOfDate = null,
        CancellationToken cancellationToken = default)
    {
        EscrowPayoutRunResultDto result = await _sender.Send(
            new RunEscrowPayoutsCommand(asOfDate),
            cancellationToken);
        return Ok(result);
    }
}
