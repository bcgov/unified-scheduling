using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Unified.Authorization;
using Unified.Authorization.Claims;
using Unified.Calendar.Conflicts;
using Unified.Calendar.Models;
using Unified.Calendar.Services;
using Unified.Calendar.Validators;

namespace Unified.Calendar.Controllers;

[ApiController]
[Authorize]
[Route("/api/calendar")]
public sealed class CalendarController(
    ILogger<CalendarController> logger,
    ICalendarEventService calendarEventService,
    ICalendarConflictService calendarConflictService,
    CalendarDataRequestValidator calendarDataRequestValidator
) : ControllerBase
{
    [HttpPost("events")]
    [ProducesResponseType(typeof(CalendarDataResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CalendarDataResponse>> GetData(
        [FromBody] CalendarDataRequest request,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation(
            "Calendar events request received for range {StartDate} to {EndDate}.",
            request.StartDate,
            request.EndDate
        );

        await calendarDataRequestValidator.ValidateAndThrowAsync(request, cancellationToken);

        var response = await calendarEventService.GetCalendarDataAsync(request, cancellationToken);

        logger.LogInformation(
            "Calendar events response completed for range {StartDate} to {EndDate} with {EventCount} events.",
            request.StartDate,
            request.EndDate,
            response.Events.Count
        );

        return Ok(response);
    }

    [HttpPost("conflicts/overrides")]
    [Authorize(Policy = AuthorizationModule.PolicyPrefix + nameof(Permissions.AssignmentsEdit))]
    [ProducesResponseType(typeof(CalendarConflictOverrideResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CalendarConflictOverrideResponse>> CreateConflictOverride(
        [FromBody] CalendarConflictOverrideRequest request,
        CancellationToken cancellationToken
    )
    {
        var userIdValue = User.FindFirst(UnifiedClaimTypes.UserId)?.Value;
        var userId = Guid.TryParse(userIdValue, out var parsedUserId) ? parsedUserId : (Guid?)null;
        return Ok(await calendarConflictService.CreateOverrideAsync(request, userId, cancellationToken));
    }
}
