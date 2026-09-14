using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Unified.Training.Models;
using Unified.Training.Services.Lookup;

namespace Unified.Training.Controllers;

[Authorize]
[ApiController]
[Tags("Training")]
[Route("api/lookup/training-profiles")]
public class TrainingProfileLookupController(ITrainingProfileLookupStrategy trainingProfileLookupStrategy)
    : ControllerBase
{
    private const string TrainingsViewPolicy = TrainingPolicies.TrainingsView;

    [HttpGet]
    [Authorize(Policy = TrainingsViewPolicy)]
    [ProducesResponseType(typeof(IEnumerable<TrainingProfileLookupResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TrainingProfileLookupResponse>>> GetAll(
        [FromQuery] bool includeExpired = false,
        CancellationToken cancellationToken = default
    )
    {
        var result = await trainingProfileLookupStrategy.GetAllAsync(includeExpired, cancellationToken);
        return Ok(result);
    }
}
