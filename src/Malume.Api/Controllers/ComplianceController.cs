using Malume.Api.Security;
using Malume.Application.Features.Compliance;
using Malume.Shared.Contracts.Auth;
using Malume.Shared.Contracts.Compliance;
using Malume.Shared.Contracts.Drivers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Malume.Api.Controllers;

[ApiController]
[Route("api/v1/compliance")]
public sealed class ComplianceController(ISender sender) : ControllerBase
{
    private readonly ISender _sender = sender;

    [HttpPost("documents")]
    [Authorize(Roles = MalumeRoles.Driver + "," + MalumeRoles.FleetOwner + "," + MalumeRoles.SchoolAdmin)]
    [ProducesResponseType<ComplianceDocumentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ComplianceDocumentDto>> SubmitDocumentAsync(
        [FromBody] SubmitComplianceDocumentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        ComplianceDocumentDto document = await _sender.Send(
            new SubmitComplianceDocumentCommand(
                request.DocumentType,
                request.DocumentNumber,
                request.StorageUrl,
                request.ExpiryDate,
                request.VehicleId),
            cancellationToken);

        return Created("/api/v1/compliance/documents/mine", document);
    }

    [HttpGet("documents/mine")]
    [Authorize(Roles = MalumeRoles.Driver + "," + MalumeRoles.FleetOwner + "," + MalumeRoles.SchoolAdmin)]
    [ProducesResponseType<IReadOnlyList<ComplianceDocumentDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ComplianceDocumentDto>>> ListMyDocumentsAsync(
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ComplianceDocumentDto> docs = await _sender.Send(
            new ListMyComplianceDocumentsQuery(),
            cancellationToken);
        return Ok(docs);
    }

    [HttpGet("review-queue")]
    [Authorize(Policy = MalumePolicies.RequireSysAdmin)]
    [ProducesResponseType<ComplianceReviewQueueDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ComplianceReviewQueueDto>> GetReviewQueueAsync(
        CancellationToken cancellationToken)
    {
        ComplianceReviewQueueDto queue = await _sender.Send(new GetComplianceReviewQueueQuery(), cancellationToken);
        return Ok(queue);
    }

    [HttpPost("documents/{documentId:guid}/verify")]
    [Authorize(Policy = MalumePolicies.RequireSysAdmin)]
    [ProducesResponseType<ComplianceDocumentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ComplianceDocumentDto>> VerifyDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        ComplianceDocumentDto doc = await _sender.Send(
            new VerifyComplianceDocumentCommand(documentId),
            cancellationToken);
        return Ok(doc);
    }

    [HttpPost("drivers/{driverProfileId:guid}/approve")]
    [Authorize(Policy = MalumePolicies.RequireSysAdmin)]
    [ProducesResponseType<DriverProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DriverProfileDto>> ApproveDriverAsync(
        Guid driverProfileId,
        CancellationToken cancellationToken)
    {
        DriverProfileDto driver = await _sender.Send(
            new ApproveDriverComplianceCommand(driverProfileId),
            cancellationToken);
        return Ok(driver);
    }

    [HttpPost("drivers/{driverProfileId:guid}/suspend")]
    [Authorize(Policy = MalumePolicies.RequireSysAdmin)]
    [ProducesResponseType<DriverProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DriverProfileDto>> SuspendDriverAsync(
        Guid driverProfileId,
        CancellationToken cancellationToken)
    {
        DriverProfileDto driver = await _sender.Send(
            new SuspendDriverCommand(driverProfileId),
            cancellationToken);
        return Ok(driver);
    }
}
