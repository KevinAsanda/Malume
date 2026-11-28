using Malume.Api.Security;
using Malume.Application.Features.Routes;
using Malume.Shared.Contracts.Auth;
using Malume.Shared.Contracts.Routes;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Malume.Api.Controllers;

[ApiController]
[Route("api/v1/routes")]
public sealed class RoutesController(ISender sender) : ControllerBase
{
    private readonly ISender _sender = sender;

    [HttpPost]
    [Authorize(Roles = MalumeRoles.FleetOwner + "," + MalumeRoles.SchoolAdmin + "," + MalumeRoles.Driver + "," + MalumeRoles.SysAdmin)]
    [ProducesResponseType<ScheduledRouteDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScheduledRouteDto>> CreateAsync(
        [FromBody] CreateScheduledRouteRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        ScheduledRouteDto route = await _sender.Send(
            new CreateScheduledRouteCommand(
                request.VehicleId,
                request.DriverProfileId,
                request.SchoolId,
                request.RouteName,
                request.Direction,
                request.CorridorWaypoints,
                request.MonthlyPriceZar),
            cancellationToken);

        return Created($"/api/v1/routes/{route.RouteId}", route);
    }

    [HttpGet]
    [Authorize]
    [ProducesResponseType<IReadOnlyList<ScheduledRouteDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ScheduledRouteDto>>> ListAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ScheduledRouteDto> routes = await _sender.Send(new GetScheduledRoutesQuery(), cancellationToken);
        return Ok(routes);
    }

    [HttpPost("{routeId:guid}/enrollments")]
    [Authorize(Roles = MalumeRoles.Parent + "," + MalumeRoles.FleetOwner + "," + MalumeRoles.SchoolAdmin + "," + MalumeRoles.SysAdmin)]
    [ProducesResponseType<RouteEnrollmentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RouteEnrollmentDto>> EnrollLearnerAsync(
        Guid routeId,
        [FromBody] EnrollLearnerOnRouteRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        RouteEnrollmentDto enrollment = await _sender.Send(
            new EnrollLearnerOnRouteCommand(routeId, request.LearnerId, request.BillingMonthStart),
            cancellationToken);

        return Created($"/api/v1/routes/{routeId}/enrollments/{enrollment.SubscriptionId}", enrollment);
    }
}
