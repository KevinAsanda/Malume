using Malume.Application.Features.Notifications;
using Malume.Shared.Contracts.Notifications;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Malume.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/notifications")]
public sealed class NotificationsController(ISender sender) : ControllerBase
{
    private readonly ISender _sender = sender;

    [HttpPost("device-token")]
    [ProducesResponseType<DeviceTokenRegistrationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DeviceTokenRegistrationDto>> RegisterDeviceTokenAsync(
        [FromBody] RegisterDeviceTokenRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        DeviceTokenRegistrationDto registration = await _sender.Send(
            new RegisterDeviceTokenCommand(request.Token, request.Platform, request.AppVariant),
            cancellationToken);

        return Ok(registration);
    }

    [HttpDelete("device-token")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UnregisterDeviceTokenAsync(
        [FromQuery] string token,
        CancellationToken cancellationToken)
    {
        await _sender.Send(new UnregisterDeviceTokenCommand(token), cancellationToken);
        return NoContent();
    }
}
