using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Unified.Training.Models;
using Unified.Training.Services;
using Unified.Training.Validators;

namespace Unified.Training.Controllers;

[ApiController]
[Authorize]
[Route("api/trainings")]
public sealed class TrainingsController(ITrainingService trainingsService, TrainingRequestValidator validator)
    : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = TrainingPolicies.TrainingsView)]
    [ProducesResponseType(typeof(IReadOnlyCollection<TrainingResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<TrainingResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await trainingsService.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [Authorize(Policy = TrainingPolicies.TrainingsView)]
    [ProducesResponseType(typeof(TrainingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrainingResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await trainingsService.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = TrainingPolicies.TrainingsCreate)]
    [ProducesResponseType(typeof(TrainingResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TrainingResponse>> Create(
        [FromBody] TrainingRequest request,
        CancellationToken cancellationToken
    )
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        var result = await trainingsService.CreateAsync(request, cancellationToken);
        return Created($"/api/trainings/{result.Id}", result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = TrainingPolicies.TrainingsEdit)]
    [ProducesResponseType(typeof(TrainingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrainingResponse>> Update(
        int id,
        [FromBody] TrainingRequest request,
        CancellationToken cancellationToken
    )
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        var result = await trainingsService.UpdateAsync(id, request, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = TrainingPolicies.TrainingsDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await trainingsService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
