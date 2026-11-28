using Malume.Api.Security;
using Malume.Application.Features.Trips;
using Malume.Shared.Contracts.Auth;
using Malume.Shared.Contracts.Trips;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Malume.Api.Controllers;

[ApiController]
[Route("api/v1/trips")]
public sealed class TripsController(ISender sender) : ControllerBase
{
    private readonly ISender _sender = sender;

    [HttpPost]
    [Authorize(Roles = MalumeRoles.Driver + "," + MalumeRoles.FleetOwner + "," + MalumeRoles.SchoolAdmin + "," + MalumeRoles.SysAdmin)]
    [ProducesResponseType<TripManifestDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TripManifestDto>> CreateDailyTripAsync(
        [FromBody] CreateDailyTripRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        TripManifestDto manifest = await _sender.Send(
            new CreateDailyTripCommand(request.RouteId, request.ServiceDate),
            cancellationToken);

        return Created($"/api/v1/trips/{manifest.TripId}/manifest", manifest);
    }

    [HttpGet("driver/schedule")]
    [Authorize(Policy = MalumePolicies.RequireDriver)]
    [ProducesResponseType<IReadOnlyList<DriverTripSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DriverTripSummaryDto>>> GetDriverScheduleAsync(
        [FromQuery] DateOnly? serviceDate = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<DriverTripSummaryDto> schedule = await _sender.Send(
            new GetDriverScheduleQuery(serviceDate),
            cancellationToken);
        return Ok(schedule);
    }

    [HttpGet("{tripId:guid}/manifest")]
    [Authorize(Roles = MalumeRoles.Driver + "," + MalumeRoles.FleetOwner + "," + MalumeRoles.SchoolAdmin + "," + MalumeRoles.SysAdmin)]
    [ProducesResponseType<TripManifestDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TripManifestDto>> GetManifestAsync(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        TripManifestDto manifest = await _sender.Send(new GetTripManifestQuery(tripId), cancellationToken);
        return Ok(manifest);
    }

    [HttpPost("{tripId:guid}/start")]
    [Authorize(Policy = MalumePolicies.RequireDriver)]
    [ProducesResponseType<DriverTripSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DriverTripSummaryDto>> StartTripAsync(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        DriverTripSummaryDto summary = await _sender.Send(new StartTripCommand(tripId), cancellationToken);
        return Ok(summary);
    }

    [HttpPost("{tripId:guid}/arrive-gate")]
    [Authorize(Policy = MalumePolicies.RequireDriver)]
    [ProducesResponseType<DriverTripSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DriverTripSummaryDto>> ArriveAtGateAsync(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        DriverTripSummaryDto summary = await _sender.Send(new ArriveAtSchoolGateCommand(tripId), cancellationToken);
        return Ok(summary);
    }

    [HttpPost("{tripId:guid}/complete")]
    [Authorize(Policy = MalumePolicies.RequireDriver)]
    [ProducesResponseType<DriverTripSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DriverTripSummaryDto>> CompleteTripAsync(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        DriverTripSummaryDto summary = await _sender.Send(new CompleteTripCommand(tripId), cancellationToken);
        return Ok(summary);
    }

    [HttpPost("{tripId:guid}/handovers/batch-sync")]
    [Authorize(Policy = MalumePolicies.RequireDriver)]
    [ProducesResponseType<BatchSyncHandoversResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<BatchSyncHandoversResponse>> BatchSyncHandoversAsync(
        Guid tripId,
        [FromBody] BatchSyncHandoversRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        BatchSyncHandoversResponse response = await _sender.Send(
            new BatchSyncHandoversCommand(tripId, request.Events),
            cancellationToken);
        return Ok(response);
    }

    [HttpPost("{tripId:guid}/sos")]
    [Authorize(Policy = MalumePolicies.RequireDriver)]
    [ProducesResponseType<SosAlertDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SosAlertDto>> TriggerSosAsync(
        Guid tripId,
        [FromBody] TriggerSosRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        SosAlertDto alert = await _sender.Send(
            new TriggerSosCommand(tripId, request.Latitude, request.Longitude, request.Notes),
            cancellationToken);
        return Accepted(alert);
    }
}
