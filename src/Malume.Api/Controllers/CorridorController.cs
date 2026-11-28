using Malume.Application.Features.Corridor;
using Malume.Domain.Common;
using Malume.Shared.Contracts.Auth;
using Malume.Shared.Contracts.Corridor;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Malume.Api.Controllers;

[ApiController]
[Route("api/v1/corridor")]
public sealed class CorridorController(ISender sender) : ControllerBase
{
    private readonly ISender _sender = sender;

    [HttpGet("options")]
    [Authorize(Roles = MalumeRoles.Parent + "," + MalumeRoles.SchoolAdmin + "," + MalumeRoles.SysAdmin)]
    [ProducesResponseType<IReadOnlyList<CorridorAdHocOptionDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<CorridorAdHocOptionDto>>> SearchCorridorOptionsAsync(
        [FromQuery] Guid schoolId,
        [FromQuery] double pickupLatitude,
        [FromQuery] double pickupLongitude,
        [FromQuery] DateOnly? serviceDate = null,
        [FromQuery] double maxDistanceMeters = GeoMath.PeakCorridorRadiusMeters,
        [FromQuery] string? direction = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<CorridorAdHocOptionDto> options = await _sender.Send(
            new SearchCorridorOptionsQuery(
                schoolId,
                serviceDate,
                pickupLatitude,
                pickupLongitude,
                maxDistanceMeters,
                direction),
            cancellationToken);

        return Ok(options);
    }

    [HttpPost("trips/{tripId:guid}/book")]
    [Authorize(Roles = MalumeRoles.Parent + "," + MalumeRoles.SysAdmin)]
    [ProducesResponseType<CorridorBookingResultDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CorridorBookingResultDto>> BookCorridorSeatAsync(
        Guid tripId,
        [FromBody] BookCorridorSeatRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        CorridorBookingResultDto result = await _sender.Send(
            new BookCorridorSeatCommand(
                tripId,
                request.LearnerId,
                request.PickupLatitude,
                request.PickupLongitude),
            cancellationToken);

        return Created($"/api/v1/trips/{tripId}/manifest", result);
    }

    [HttpGet("off-peak-drivers")]
    [Authorize(Roles = MalumeRoles.Parent + "," + MalumeRoles.SysAdmin)]
    [ProducesResponseType<IReadOnlyList<OffPeakDriverOptionDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<OffPeakDriverOptionDto>>> SearchOffPeakDriversAsync(
        [FromQuery] double pickupLatitude,
        [FromQuery] double pickupLongitude,
        [FromQuery] double maxDistanceMeters = GeoMath.OffPeakSearchRadiusMeters,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<OffPeakDriverOptionDto> drivers = await _sender.Send(
            new SearchOffPeakDriversQuery(pickupLatitude, pickupLongitude, maxDistanceMeters),
            cancellationToken);

        return Ok(drivers);
    }
}
