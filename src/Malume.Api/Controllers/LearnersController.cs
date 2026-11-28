using Malume.Application.Features.Learners;
using Malume.Shared.Contracts.Auth;
using Malume.Shared.Contracts.Learners;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Malume.Api.Controllers;

[ApiController]
[Route("api/v1/learners")]
public sealed class LearnersController(ISender sender) : ControllerBase
{
    private readonly ISender _sender = sender;

    [HttpPost]
    [Authorize(Roles = MalumeRoles.Parent + "," + MalumeRoles.SysAdmin)]
    [ProducesResponseType<LearnerDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LearnerDto>> CreateAsync(
        [FromBody] CreateLearnerRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        LearnerDto learner = await _sender.Send(
            new CreateLearnerCommand(
                request.SchoolId,
                request.FullName,
                request.Grade,
                request.HomeLatitude,
                request.HomeLongitude,
                request.HomeAddressText,
                request.EmergencyContactPhone),
            cancellationToken);

        return Created($"/api/v1/learners/{learner.LearnerId}", learner);
    }

    [HttpGet]
    [Authorize(Roles = MalumeRoles.Parent + "," + MalumeRoles.SysAdmin)]
    [ProducesResponseType<IReadOnlyList<LearnerDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LearnerDto>>> ListAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<LearnerDto> learners = await _sender.Send(new GetLearnersQuery(), cancellationToken);
        return Ok(learners);
    }

    [HttpGet("{learnerId:guid}/qr-badge")]
    [Authorize(Roles = MalumeRoles.Parent + "," + MalumeRoles.SchoolAdmin + "," + MalumeRoles.SysAdmin)]
    [ProducesResponseType<LearnerQrBadgeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LearnerQrBadgeDto>> GetQrBadgeAsync(
        Guid learnerId,
        CancellationToken cancellationToken)
    {
        LearnerQrBadgeDto badge = await _sender.Send(new GetLearnerQrBadgeQuery(learnerId), cancellationToken);
        return Ok(badge);
    }

    [HttpGet("{learnerId:guid}/daily-pin")]
    [Authorize(Roles = MalumeRoles.Parent + "," + MalumeRoles.SysAdmin)]
    [ProducesResponseType<DailyPinDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DailyPinDto>> GetDailyPinAsync(
        Guid learnerId,
        [FromQuery] DateOnly? serviceDate = null,
        [FromQuery] string direction = "MorningToSchool",
        CancellationToken cancellationToken = default)
    {
        DailyPinDto pin = await _sender.Send(
            new GetLearnerDailyPinQuery(learnerId, serviceDate, direction),
            cancellationToken);
        return Ok(pin);
    }
}
