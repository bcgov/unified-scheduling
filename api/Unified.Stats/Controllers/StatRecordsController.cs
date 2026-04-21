using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Unified.Common.Validation;
using Unified.Stats.Models;
using Unified.Stats.Services;
using Unified.Stats.Validators;

namespace Unified.Stats.Controllers;

[ApiController]
[Route("api/stats/records")]
[Authorize]
public class StatRecordsController(IStatRecordService service, StatRecordRequestValidator validator) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<StatRecordResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<StatRecordResponse>>> GetAll(
        [FromQuery] StatRecordQueryParams? queryParams,
        CancellationToken cancellationToken
    )
    {
        return Ok(await service.GetAllAsync(queryParams, cancellationToken));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(StatRecordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StatRecordResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await service.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(StatRecordResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StatRecordResponse>> Create(
        [FromBody] StatRecordRequest request,
        CancellationToken cancellationToken
    )
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return ValidationProblem(new ValidationProblemDetails(validation.ToValidationErrors()));

        var result = await service.CreateAsync(request, cancellationToken);
        return Created($"/api/stats/records/{result.Id}", result);
    }

    [HttpPost("batch")]
    [ProducesResponseType(typeof(IEnumerable<StatRecordResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<StatRecordResponse>>> CreateBatch(
        [FromBody] IEnumerable<StatRecordRequest> requests,
        CancellationToken cancellationToken
    )
    {
        var requestList = requests.ToList();

        foreach (var request in requestList)
        {
            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
                return ValidationProblem(new ValidationProblemDetails(validation.ToValidationErrors()));
        }

        var result = await service.CreateBatchAsync(requestList, cancellationToken);
        return Created("/api/stats/records", result);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(StatRecordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StatRecordResponse>> Update(
        int id,
        [FromBody] StatRecordRequest request,
        CancellationToken cancellationToken
    )
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return ValidationProblem(new ValidationProblemDetails(validation.ToValidationErrors()));

        var result = await service.UpdateAsync(id, request, cancellationToken);
        return result is null ? NotFound() : Ok(result);
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
