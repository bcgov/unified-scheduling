using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Unified.Common.Validation;
using Unified.Stats.Models;
using Unified.Stats.Services;
using Unified.Stats.Validators;

namespace Unified.Stats.Controllers;

[ApiController]
[Route("api/stats/signoffs")]
[Authorize]
public class StatSignoffsController(IStatSignoffService service, StatSignoffRequestValidator validator) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<StatSignoffResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<StatSignoffResponse>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await service.GetAllAsync(cancellationToken));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(StatSignoffResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StatSignoffResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await service.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(StatSignoffResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StatSignoffResponse>> Create([FromBody] StatSignoffRequest request, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return ValidationProblem(new ValidationProblemDetails(validation.ToValidationErrors()));

        var result = await service.CreateAsync(request, cancellationToken);
        return Created($"/api/stats/signoffs/{result.Id}", result);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await service.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
