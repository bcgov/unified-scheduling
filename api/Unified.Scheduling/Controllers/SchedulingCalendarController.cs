using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Unified.Scheduling.Models;
using Unified.Scheduling.Services;
using Unified.Scheduling.Validators;

namespace Unified.Scheduling.Controllers;

[ApiController]
[Authorize]
[Route("api/scheduling/calendar")]
public sealed class SchedulingCalendarController(
    IShiftService shiftService,
    SchedulingCalendarRequestValidator schedulingCalendarRequestValidator
) : ControllerBase
{
    [HttpPost("events")]
    [Authorize(Policy = SchedulingPolicies.ShiftsView)]
    [ProducesResponseType(typeof(SchedulingCalendarDataResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SchedulingCalendarDataResponse>> GetData(
        [FromBody] SchedulingCalendarRequest request,
        CancellationToken cancellationToken
    )
    {
        await schedulingCalendarRequestValidator.ValidateAndThrowAsync(request, cancellationToken);

        return Ok(await shiftService.GetSchedulingCalendarDataAsync(request, cancellationToken));
    }
}
