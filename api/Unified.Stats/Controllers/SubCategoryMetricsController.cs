using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Unified.Stats.Models;
using Unified.Stats.Services;

namespace Unified.Stats.Controllers;

[ApiController]
[Route("api/stats/sub-category-metrics")]
[Authorize]
public class SubCategoryMetricsController(ISubCategoryMetricService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<SubCategoryMetricResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SubCategoryMetricResponse>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await service.GetAllAsync(cancellationToken));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(SubCategoryMetricResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubCategoryMetricResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await service.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
