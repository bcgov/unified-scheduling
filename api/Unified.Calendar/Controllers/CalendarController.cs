using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
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
    CalendarEventsRequestValidator calendarEventsRequestValidator
) : ControllerBase
{
    [HttpPost("events")]
    [ProducesResponseType(typeof(IReadOnlyCollection<CalendarEventResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<CalendarEventResponse>>> GetEvents(
        [FromBody] CalendarEventsRequest request,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation(
            "Calendar events request received for range {StartDate} to {EndDate}.",
            request.StartDate,
            request.EndDate
        );

        try
        {
            await calendarEventsRequestValidator.ValidateAndThrowAsync(request, cancellationToken);
        }
        catch (ValidationException)
        {
            logger.LogWarning(
                "Calendar events request validation failed for range {StartDate} to {EndDate}.",
                request.StartDate,
                request.EndDate
            );

            throw;
        }

        var events = await calendarEventService.GetEventsAsync(request, cancellationToken);

        logger.LogInformation(
            "Calendar events response completed for range {StartDate} to {EndDate} with {EventCount} events.",
            request.StartDate,
            request.EndDate,
            events.Count
        );

        return Ok(events);
    }
}
