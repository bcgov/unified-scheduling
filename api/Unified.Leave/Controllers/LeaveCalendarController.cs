using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Unified.Leave.Models.Calendar;
using Unified.Leave.Services;
using Unified.Leave.Validators;

namespace Unified.Leave.Controllers;

[ApiController]
[Authorize(Policy = LeavePolicies.LeaveView)]
[Route("api/leave/calendar/events")]
public sealed class LeaveCalendarController(ILeaveCalendarDataProvider provider, LeaveCalendarQueryValidator validator)
    : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(IReadOnlyCollection<LeaveCalendarEvent>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<LeaveCalendarEvent>>> GetData(
        [FromBody] CalendarEventQueryContext request,
        CancellationToken cancellationToken
    )
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var events = await provider.GetEventsAsync(request, cancellationToken);
        return Ok(
            events.Select(item => item with { UserIds = item.UserIds.Intersect(request.UserIds).ToList() }).ToList()
        );
    }
}
