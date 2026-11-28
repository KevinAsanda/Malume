using Malume.Api.Security;
using Malume.Application.Features.Fleets;
using Malume.Shared.Contracts.Drivers;
using Malume.Shared.Contracts.Tenants;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Malume.Api.Controllers;

[ApiController]
[Route("api/v1/fleets")]
[Authorize(Policy = MalumePolicies.RequireFleetOwner)]
public sealed class FleetsController(ISender sender) : ControllerBase
{
    private readonly ISender _sender = sender;

    [HttpPost]
    [ProducesResponseType<FleetDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FleetDto>> CreateFleetAsync(
        [FromBody] CreateFleetRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        FleetDto fleet = await _sender.Send(
            new CreateFleetCommand(request.Name, request.CompanyRegistrationNumber),
            cancellationToken);

        return Created("/api/v1/fleets/mine", fleet);
    }

    [HttpGet("mine")]
    [ProducesResponseType<FleetDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FleetDto>> GetMyFleetAsync(CancellationToken cancellationToken)
    {
        FleetDto fleet = await _sender.Send(new GetMyFleetQuery(), cancellationToken);
        return Ok(fleet);
    }

    [HttpGet("mine/drivers")]
    [ProducesResponseType<IReadOnlyList<DriverProfileDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DriverProfileDto>>> ListFleetDriversAsync(
        CancellationToken cancellationToken)
    {
        IReadOnlyList<DriverProfileDto> drivers = await _sender.Send(new ListFleetDriversQuery(), cancellationToken);
        return Ok(drivers);
    }

    [HttpPost("mine/drivers")]
    [ProducesResponseType<DriverProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DriverProfileDto>> HireDriverAsync(
        [FromBody] HireDriverRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        DriverProfileDto driver = await _sender.Send(new HireDriverCommand(request.DriverEmail), cancellationToken);
        return Ok(driver);
    }
}
