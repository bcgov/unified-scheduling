using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Unified.Training.Models;
using Unified.Training.Services;

namespace Unified.Training.Controllers;

[Authorize]
[ApiController]
[Tags("Training")]
[Route("api/training/profile-types")]
public class TrainingProfileTypesController(ITrainingProfileTypeService trainingProfileTypeService) : ControllerBase
{
    private const string TrainingsViewPolicy = TrainingPolicies.TrainingsView;

    [HttpGet]
    [Authorize(Policy = TrainingsViewPolicy)]
    [ProducesResponseType(typeof(IEnumerable<TrainingProfileTypeResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TrainingProfileTypeResponse>>> GetAll(
        CancellationToken cancellationToken = default
    )
    {
        var result = await trainingProfileTypeService.GetAllAsync(cancellationToken);
        return Ok(result);
    }
}
