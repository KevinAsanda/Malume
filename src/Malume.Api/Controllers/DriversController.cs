using Malume.Api.Security;
using Malume.Application.Features.Drivers;
using Malume.Shared.Contracts.Drivers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Malume.Api.Controllers;

[ApiController]
[Route("api/v1/drivers")]
public sealed class DriversController(ISender sender) : ControllerBase
{
    private readonly ISender _sender = sender;

    [HttpPost("me")]
    [Authorize(Policy = MalumePolicies.RequireDriver)]
    [ProducesResponseType<DriverProfileDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DriverProfileDto>> CreateMyProfileAsync(
        [FromBody] CreateDriverProfileRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        DriverProfileDto profile = await _sender.Send(
            new CreateMyDriverProfileCommand(
                request.PrdpNumber,
                request.PrdpExpiryDate,
                request.PoliceClearanceExpiryDate),
            cancellationToken);

        return Created("/api/v1/drivers/me", profile);
    }

    [HttpGet("me")]
    [Authorize(Policy = MalumePolicies.RequireDriver)]
    [ProducesResponseType<DriverProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DriverProfileDto>> GetMyProfileAsync(CancellationToken cancellationToken)
    {
        DriverProfileDto profile = await _sender.Send(new GetMyDriverProfileQuery(), cancellationToken);
        return Ok(profile);
    }

    [HttpGet]
    [Authorize(Policy = MalumePolicies.RequirePortalOperator)]
    [ProducesResponseType<IReadOnlyList<DriverProfileDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DriverProfileDto>>> ListDriversAsync(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<DriverProfileDto> drivers = await _sender.Send(new ListDriversQuery(status), cancellationToken);
        return Ok(drivers);
    }
}
