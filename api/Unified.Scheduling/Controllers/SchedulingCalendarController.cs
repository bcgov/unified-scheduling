using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Unified.Authorization;
using Unified.Authorization.Claims;
using Unified.Scheduling.Models;
using Unified.Scheduling.Services;
using Unified.Scheduling.Validators;

namespace Unified.Scheduling.Controllers;

[ApiController]
[Authorize]
[Route("api/scheduling/calendar")]
public sealed class SchedulingCalendarController(
    ISchedulingCalendarService schedulingCalendarService,
    SchedulingCalendarRequestValidator schedulingCalendarRequestValidator
) : ControllerBase
{
    [HttpPost("events")]
    [ProducesResponseType(typeof(SchedulingCalendarDataResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SchedulingCalendarDataResponse>> GetData(
        [FromBody] SchedulingCalendarRequest request,
        CancellationToken cancellationToken
    )
    {
        await schedulingCalendarRequestValidator.ValidateAndThrowAsync(request, cancellationToken);

        var canViewShifts = User.HasClaim(UnifiedClaimTypes.Permission, Permissions.ShiftsView.ToString());
        var canViewAssignments = User.HasClaim(UnifiedClaimTypes.Permission, Permissions.AssignmentsView.ToString());
        if (!canViewShifts && !canViewAssignments)
            return Forbid();
        return Ok(
            await schedulingCalendarService.GetDataAsync(request, canViewShifts, canViewAssignments, cancellationToken)
        );
    }
}
