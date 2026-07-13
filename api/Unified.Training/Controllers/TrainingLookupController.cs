using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Unified.Core.Models;
using Unified.Core.Services;

namespace Unified.Training.Controllers;

[Authorize]
[ApiController]
[Route("api/lookup/trainings")]
public class TrainingLookupController(ITrainingLookupService trainingLookupService) : ControllerBase
{
    private const string TrainingsViewPolicy = "Permission:TrainingsView";
    private const string TrainingsCreatePolicy = "Permission:TrainingsCreate";
    private const string TrainingsEditPolicy = "Permission:TrainingsEdit";

    [HttpGet]
    [Authorize(Policy = TrainingsViewPolicy)]
    [ProducesResponseType(typeof(IEnumerable<TrainingLookupResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TrainingLookupResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await trainingLookupService.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [Authorize(Policy = TrainingsViewPolicy)]
    [ProducesResponseType(typeof(TrainingLookupResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrainingLookupResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await trainingLookupService.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = TrainingsCreatePolicy)]
    [ProducesResponseType(typeof(TrainingLookupResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<TrainingLookupResponse>> Create(
        [FromBody] TrainingLookupRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await trainingLookupService.CreateAsync(request, cancellationToken);
        return Created($"/api/lookup/trainings/{result.Id}", result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = TrainingsEditPolicy)]
    [ProducesResponseType(typeof(TrainingLookupResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrainingLookupResponse>> Update(
        int id,
        [FromBody] TrainingLookupRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await trainingLookupService.UpdateAsync(id, request, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPatch("{id:int}/order")]
    [Authorize(Policy = TrainingsEditPolicy)]
    [ProducesResponseType(typeof(TrainingLookupResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrainingLookupResponse>> MoveOrder(
        int id,
        [FromBody] TrainingLookupMoveOrderRequest request,
        CancellationToken cancellationToken
    )
    {
        if (request.NewOrder < 0)
        {
            return BadRequest(
                new ProblemDetails { Title = "Invalid order", Detail = "newOrder must be greater than or equal to 0." }
            );
        }

        var result = await trainingLookupService.MoveOrderAsync(id, request.NewOrder, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
