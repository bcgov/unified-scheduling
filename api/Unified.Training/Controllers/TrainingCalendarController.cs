using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Unified.Training.Models.Calendar;
using Unified.Training.Services;
using Unified.Training.Validators;

namespace Unified.Training.Controllers;

[ApiController]
[Authorize]
[Route("api/training/calendar")]
public sealed class TrainingCalendarController(
    TrainingCalendarEventService service,
    TrainingCalendarEventsRequestValidator validator
) : ControllerBase
{
    [HttpPost("events")]
    [Authorize(Policy = TrainingPolicies.UserTrainingsView)]
    [ProducesResponseType(typeof(IReadOnlyCollection<TrainingCalendarEvent>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyCollection<TrainingCalendarEvent>>> GetEvents(
        [FromBody] TrainingCalendarEventsRequest request,
        CancellationToken cancellationToken
    )
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        return Ok(await service.GetEventsAsync(request, cancellationToken));
    }
}
