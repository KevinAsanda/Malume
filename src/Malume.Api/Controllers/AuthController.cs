using Malume.Api.Security;
using Malume.Application.Features.Auth;
using Malume.Shared.Contracts.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Malume.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public sealed class AuthController(ISender sender) : ControllerBase
{
    private readonly ISender _sender = sender;

    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> RegisterAsync(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var command = new RegisterUserCommand(
            request.Email,
            request.FullName,
            request.PhoneNumber,
            request.Password,
            request.Role,
            request.TenantFleetId,
            request.TenantSchoolId);

        AuthResponse response = await _sender.Send(command, cancellationToken);
        return Created("/api/v1/auth/me", response);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> LoginAsync(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        AuthResponse response = await _sender.Send(new LoginCommand(request.Email, request.Password), cancellationToken);
        return Ok(response);
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<UserClaimsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserClaimsResponse>> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        UserClaimsResponse response = await _sender.Send(new GetCurrentUserQuery(), cancellationToken);
        return Ok(response);
    }

    [HttpGet("rbac/fleet")]
    [Authorize(Policy = MalumePolicies.RequireFleetOwner)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult CheckFleetAccess() => Ok(new { role = MalumeRoles.FleetOwner, authorized = true });

    [HttpGet("rbac/sysadmin")]
    [Authorize(Policy = MalumePolicies.RequireSysAdmin)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult CheckSysAdminAccess() => Ok(new { role = MalumeRoles.SysAdmin, authorized = true });
}
