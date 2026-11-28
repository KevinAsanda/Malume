using Malume.Api.Security;
using Malume.Application.Features.Endorsements;
using Malume.Application.Features.Schools;
using Malume.Shared.Contracts.Auth;
using Malume.Shared.Contracts.Endorsements;
using Malume.Shared.Contracts.Tenants;
using Malume.Shared.Contracts.Trips;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Malume.Api.Controllers;

[ApiController]
[Route("api/v1/schools")]
public sealed class SchoolsController(ISender sender) : ControllerBase
{
    private readonly ISender _sender = sender;

    [HttpGet]
    [Authorize]
    [ProducesResponseType<IReadOnlyList<SchoolDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SchoolDto>>> ListSchoolsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<SchoolDto> schools = await _sender.Send(new ListSchoolsQuery(), cancellationToken);
        return Ok(schools);
    }

    [HttpPost]
    [Authorize(Policy = MalumePolicies.RequireSchoolAdmin)]
    [ProducesResponseType<SchoolDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SchoolDto>> CreateSchoolAsync(
        [FromBody] CreateSchoolRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        SchoolDto school = await _sender.Send(
            new CreateSchoolCommand(
                request.Name,
                request.AddressText,
                request.GateLatitude,
                request.GateLongitude,
                request.EmisNumber,
                request.GateGeofenceRadiusMeters),
            cancellationToken);

        return Created("/api/v1/schools/mine", school);
    }

    [HttpGet("mine")]
    [Authorize(Policy = MalumePolicies.RequireSchoolAdmin)]
    [ProducesResponseType<SchoolDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SchoolDto>> GetMySchoolAsync(CancellationToken cancellationToken)
    {
        SchoolDto school = await _sender.Send(new GetMySchoolQuery(), cancellationToken);
        return Ok(school);
    }

    [HttpGet("mine/endorsements")]
    [Authorize(Policy = MalumePolicies.RequireSchoolAdmin)]
    [ProducesResponseType<IReadOnlyList<SchoolEndorsementDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SchoolEndorsementDto>>> ListEndorsementsAsync(
        CancellationToken cancellationToken)
    {
        IReadOnlyList<SchoolEndorsementDto> endorsements = await _sender.Send(
            new ListMySchoolEndorsementsQuery(),
            cancellationToken);
        return Ok(endorsements);
    }

    [HttpPost("mine/endorsements")]
    [Authorize(Policy = MalumePolicies.RequireSchoolAdmin)]
    [ProducesResponseType<SchoolEndorsementDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SchoolEndorsementDto>> EndorseDriverAsync(
        [FromBody] EndorseDriverRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        SchoolEndorsementDto endorsement = await _sender.Send(
            new EndorseDriverCommand(request.DriverProfileId, request.Notes),
            cancellationToken);

        return Ok(endorsement);
    }

    [HttpDelete("mine/endorsements/{driverProfileId:guid}")]
    [Authorize(Policy = MalumePolicies.RequireSchoolAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeEndorsementAsync(
        Guid driverProfileId,
        CancellationToken cancellationToken)
    {
        await _sender.Send(new RevokeEndorsementCommand(driverProfileId), cancellationToken);
        return NoContent();
    }

    [HttpGet("{schoolId:guid}/gate-board")]
    [Authorize(Roles = MalumeRoles.SchoolAdmin + "," + MalumeRoles.SysAdmin)]
    [ProducesResponseType<SchoolGateBoardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SchoolGateBoardDto>> GetGateBoardAsync(
        Guid schoolId,
        [FromQuery] DateOnly? serviceDate = null,
        CancellationToken cancellationToken = default)
    {
        SchoolGateBoardDto board = await _sender.Send(
            new GetSchoolGateBoardQuery(schoolId, serviceDate),
            cancellationToken);
        return Ok(board);
    }
}
