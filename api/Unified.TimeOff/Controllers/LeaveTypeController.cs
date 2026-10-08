using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Unified.TimeOff.Models;
using Unified.TimeOff.Services;
using Unified.TimeOff.Validators;

namespace Unified.TimeOff.Controllers;

[ApiController]
[Authorize]
[Route("api/leave/leave-types")]
public sealed class LeaveTypeController(
    ILeaveTypeService leaveTypeService,
    LeaveTypeRequestValidator leaveTypeRequestValidator
) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = LeavePolicies.LeaveTypesView)]
    [ProducesResponseType(typeof(IEnumerable<LeaveTypeResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<LeaveTypeResponse>>> GetLeaveTypes(
        CancellationToken cancellationToken
    ) => Ok(await leaveTypeService.GetLeaveTypesAsync(cancellationToken));

    [HttpGet("{id:int}")]
    [Authorize(Policy = LeavePolicies.LeaveTypesView)]
    [ProducesResponseType(typeof(LeaveTypeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeaveTypeResponse>> GetLeaveTypeById(int id, CancellationToken cancellationToken)
    {
        var result = await leaveTypeService.GetLeaveTypeByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = TimeOffPolicies.TimeOffCreateAndAssign)]
    [ProducesResponseType(typeof(LeaveTypeResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LeaveTypeResponse>> CreateLeaveType(
        [FromBody] LeaveTypeRequest request,
        CancellationToken cancellationToken
    )
    {
        await leaveTypeRequestValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await leaveTypeService.CreateLeaveTypeAsync(request, cancellationToken);
        return Created($"/api/leave/leave-types/{result.Id}", result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = TimeOffPolicies.TimeOffEdit)]
    [ProducesResponseType(typeof(LeaveTypeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LeaveTypeResponse>> UpdateLeaveType(
        int id,
        [FromBody] LeaveTypeRequest request,
        CancellationToken cancellationToken
    )
    {
        await leaveTypeRequestValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await leaveTypeService.UpdateLeaveTypeAsync(id, request, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = TimeOffPolicies.TimeOffDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteLeaveType(int id, CancellationToken cancellationToken) =>
        await leaveTypeService.DeleteLeaveTypeAsync(id, cancellationToken) ? NoContent() : NotFound();
}
