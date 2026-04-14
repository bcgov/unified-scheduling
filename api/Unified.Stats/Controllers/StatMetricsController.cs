using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Unified.Stats.Models;
using Unified.Stats.Services;

namespace Unified.Stats.Controllers;

[ApiController]
[Route("api/stats/metrics")]
[Authorize]
public class StatMetricsController(IStatMetricService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<StatMetricResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<StatMetricResponse>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await service.GetAllAsync(cancellationToken));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(StatMetricResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StatMetricResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await service.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
