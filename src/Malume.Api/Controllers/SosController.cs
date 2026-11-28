using Malume.Application.Features.Sos;
using Malume.Shared.Contracts.Auth;
using Malume.Shared.Contracts.Trips;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Malume.Api.Controllers;

[ApiController]
[Route("api/v1/sos")]
public sealed class SosController(ISender sender) : ControllerBase
{
    private readonly ISender _sender = sender;

    [HttpGet]
    [Authorize(Roles = MalumeRoles.SysAdmin + "," + MalumeRoles.SchoolAdmin + "," + MalumeRoles.FleetOwner)]
    [ProducesResponseType<IReadOnlyList<SosAlertDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SosAlertDto>>> ListIncidentsAsync(
        [FromQuery] bool activeOnly = false,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SosAlertDto> incidents = await _sender.Send(
            new GetSosIncidentsQuery(activeOnly),
            cancellationToken);
        return Ok(incidents);
    }

    [HttpPost("{incidentId:guid}/acknowledge")]
    [Authorize(Roles = MalumeRoles.SysAdmin + "," + MalumeRoles.SchoolAdmin)]
    [ProducesResponseType<SosAlertDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SosAlertDto>> AcknowledgeAsync(
        Guid incidentId,
        CancellationToken cancellationToken)
    {
        SosAlertDto updated = await _sender.Send(new AcknowledgeSosIncidentCommand(incidentId), cancellationToken);
        return Ok(updated);
    }

    [HttpPost("{incidentId:guid}/resolve")]
    [Authorize(Roles = MalumeRoles.SysAdmin + "," + MalumeRoles.SchoolAdmin)]
    [ProducesResponseType<SosAlertDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SosAlertDto>> ResolveAsync(
        Guid incidentId,
        [FromBody] ResolveSosIncidentRequest? request,
        CancellationToken cancellationToken)
    {
        SosAlertDto updated = await _sender.Send(
            new ResolveSosIncidentCommand(incidentId, request?.ResolutionNotes),
            cancellationToken);
        return Ok(updated);
    }
}
