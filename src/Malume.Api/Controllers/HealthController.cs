using Malume.Infrastructure.Persistence;
using Malume.Shared.Contracts.Health;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Malume.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public sealed class HealthController(MalumeDbContext dbContext) : ControllerBase
{
    private readonly MalumeDbContext _dbContext = dbContext;

    [HttpGet]
    [ProducesResponseType<HealthStatusResponse>(StatusCodes.Status200OK)]
    public ActionResult<HealthStatusResponse> Get()
    {
        return Ok(new HealthStatusResponse("Malume.Api", "Healthy", DateTimeOffset.UtcNow));
    }

    [HttpGet("database")]
    [ProducesResponseType<HealthStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<HealthStatusResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthStatusResponse>> GetDatabaseHealthAsync(CancellationToken cancellationToken)
    {
        bool canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
        if (!canConnect)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new HealthStatusResponse("Malume.Api.Database", "Unhealthy", DateTimeOffset.UtcNow));
        }

        return Ok(new HealthStatusResponse("Malume.Api.Database", "Healthy", DateTimeOffset.UtcNow));
    }
}
