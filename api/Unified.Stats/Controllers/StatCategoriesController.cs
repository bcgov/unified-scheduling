using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Unified.Stats.Models;
using Unified.Stats.Services;

namespace Unified.Stats.Controllers;

[ApiController]
[Route("api/stats/categories")]
[Authorize]
public class StatCategoriesController(IStatCategoryService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<StatCategoryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<StatCategoryResponse>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await service.GetAllAsync(cancellationToken));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(StatCategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StatCategoryResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await service.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
