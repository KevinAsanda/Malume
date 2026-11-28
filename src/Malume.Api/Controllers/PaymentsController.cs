using System.Text;
using Malume.Api.Security;
using Malume.Application.Features.Escrow;
using Malume.Shared.Contracts.Auth;
using Malume.Shared.Contracts.Escrow;
using Malume.Shared.Contracts.Tenants;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Malume.Api.Controllers;

[ApiController]
[Route("api/v1/payments")]
public sealed class PaymentsController(ISender sender) : ControllerBase
{
    private readonly ISender _sender = sender;

    [HttpPost("subscriptions/initialize")]
    [Authorize(Roles = MalumeRoles.Parent + "," + MalumeRoles.SysAdmin)]
    [ProducesResponseType<InitializeSubscriptionPaymentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<InitializeSubscriptionPaymentResponse>> InitializeSubscriptionAsync(
        [FromBody] InitializeSubscriptionPaymentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        InitializeSubscriptionPaymentResponse response = await _sender.Send(
            new InitializeSubscriptionPaymentCommand(
                request.RouteId,
                request.LearnerId,
                request.BillingMonthStart,
                request.CallbackUrl),
            cancellationToken);

        return Ok(response);
    }

    [HttpPost("subscriptions/{reference}/verify")]
    [Authorize(Roles = MalumeRoles.Parent + "," + MalumeRoles.SysAdmin)]
    [ProducesResponseType<VerifySubscriptionPaymentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VerifySubscriptionPaymentResponse>> VerifySubscriptionAsync(
        string reference,
        CancellationToken cancellationToken)
    {
        VerifySubscriptionPaymentResponse response = await _sender.Send(
            new VerifySubscriptionPaymentCommand(reference),
            cancellationToken);

        return Ok(response);
    }

    [HttpPost("paystack/webhook")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> HandleWebhookAsync(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        string rawPayload = await reader.ReadToEndAsync(cancellationToken);
        string signature = Request.Headers["x-paystack-signature"].ToString();

        await _sender.Send(new ProcessPaystackWebhookCommand(rawPayload, signature), cancellationToken);
        return Ok(new { received = true });
    }

    [HttpPost("fleets/mine/subaccount")]
    [Authorize(Policy = MalumePolicies.RequireFleetOwner)]
    [ProducesResponseType<FleetDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FleetDto>> ConfigureFleetSubaccountAsync(
        [FromBody] ConfigureFleetSubaccountRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        FleetDto fleet = await _sender.Send(
            new ConfigureFleetSubaccountCommand(
                request.BusinessName,
                request.SettlementBankCode,
                request.AccountNumber,
                request.PercentageCharge),
            cancellationToken);

        return Ok(fleet);
    }
}
