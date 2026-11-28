using Malume.Api.Security;
using Malume.Application.Features.Vehicles;
using Malume.Shared.Contracts.Auth;
using Malume.Shared.Contracts.Vehicles;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Malume.Api.Controllers;

[ApiController]
[Route("api/v1/vehicles")]
public sealed class VehiclesController(ISender sender) : ControllerBase
{
    private readonly ISender _sender = sender;

    [HttpPost]
    [Authorize(Roles = MalumeRoles.FleetOwner + "," + MalumeRoles.SchoolAdmin)]
    [ProducesResponseType<VehicleDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<VehicleDto>> RegisterVehicleAsync(
        [FromBody] CreateVehicleRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        VehicleDto vehicle = await _sender.Send(
            new RegisterVehicleCommand(
                request.RegistrationNumber,
                request.MakeModel,
                request.TotalSeatCapacity,
                request.LicenseDiscExpiryDate,
                request.CofExpiryDate),
            cancellationToken);

        return Created("/api/v1/vehicles", vehicle);
    }

    [HttpGet]
    [Authorize(Policy = MalumePolicies.RequirePortalOperator)]
    [ProducesResponseType<IReadOnlyList<VehicleDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<VehicleDto>>> ListVehiclesAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<VehicleDto> vehicles = await _sender.Send(new ListMyVehiclesQuery(), cancellationToken);
        return Ok(vehicles);
    }

    [HttpPut("{vehicleId:guid}/driver")]
    [Authorize(Roles = MalumeRoles.FleetOwner + "," + MalumeRoles.SchoolAdmin)]
    [ProducesResponseType<VehicleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VehicleDto>> AssignDriverAsync(
        Guid vehicleId,
        [FromBody] AssignVehicleDriverRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        VehicleDto vehicle = await _sender.Send(
            new AssignVehicleDriverCommand(vehicleId, request.DriverProfileId),
            cancellationToken);

        return Ok(vehicle);
    }
}
