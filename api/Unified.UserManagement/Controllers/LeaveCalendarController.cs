using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Unified.UserManagement.Models;
using Unified.UserManagement.Services;
using Unified.UserManagement.Validators;

namespace Unified.UserManagement.Controllers;

[ApiController]
[Authorize]
[Route("api/leave/calendar")]
public sealed class LeaveCalendarController(
    ILeaveCalendarService leaveCalendarService,
    LeaveCalendarRequestValidator leaveCalendarRequestValidator
) : ControllerBase
{
    [HttpPost("events")]
    [Authorize(Policy = UserManagementPolicies.LeaveView)]
    [ProducesResponseType(typeof(LeaveCalendarDataResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LeaveCalendarDataResponse>> GetData(
        [FromBody] LeaveCalendarRequest request,
        CancellationToken cancellationToken
    )
    {
        await leaveCalendarRequestValidator.ValidateAndThrowAsync(request, cancellationToken);

        return Ok(await leaveCalendarService.GetLeaveCalendarDataAsync(request, cancellationToken));
    }
}
