using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Unified.Leave.Models.Scheduling;
using Unified.Leave.Services;
using Unified.Leave.Validators;

namespace Unified.Leave.Controllers;

[ApiController]
[Authorize]
[Route("api/scheduling/leave")]
public sealed class SchedulingLeaveController(
    ISchedulingLeaveService leaveService,
    SchedulingLeaveRequestValidator leaveEntryRequestValidator,
    SchedulingLeaveSeriesRequestValidator leaveSeriesRequestValidator
) : ControllerBase
{
    [HttpGet("users/{userId:guid}/entries")]
    [Authorize(Policy = LeavePolicies.LeaveViewOrUser)]
    [ProducesResponseType(typeof(IReadOnlyList<SchedulingLeaveEntryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SchedulingLeaveEntryResponse>>> GetLeaveEntriesByUser(
        Guid userId,
        CancellationToken cancellationToken
    ) => Ok(await leaveService.GetLeaveEntriesByUserAsync(userId, cancellationToken));

    [HttpGet("users/{userId:guid}/series")]
    [Authorize(Policy = LeavePolicies.LeaveViewOrUser)]
    [ProducesResponseType(typeof(IReadOnlyList<SchedulingLeaveSeriesResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SchedulingLeaveSeriesResponse>>> GetLeaveSeriesByUser(
        Guid userId,
        CancellationToken cancellationToken
    ) => Ok(await leaveService.GetLeaveSeriesByUserAsync(userId, cancellationToken));

    [HttpGet("series/{id:int}")]
    [Authorize(Policy = LeavePolicies.LeaveViewOrUser)]
    [ProducesResponseType(typeof(SchedulingLeaveSeriesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SchedulingLeaveSeriesResponse>> GetLeaveSeriesById(
        int id,
        CancellationToken cancellationToken
    )
    {
        var result = await leaveService.GetLeaveSeriesByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("series")]
    [Authorize(Policy = LeavePolicies.LeaveCreateOrUser)]
    [ProducesResponseType(typeof(SchedulingLeaveSeriesResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SchedulingLeaveSeriesResponse>> CreateLeaveSeries(
        [FromBody] SchedulingLeaveSeriesRequest request,
        CancellationToken cancellationToken
    )
    {
        await leaveSeriesRequestValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await leaveService.CreateLeaveSeriesAsync(request, cancellationToken);
        return Created($"/api/scheduling/leave/series/{result.Id}", result);
    }

    [HttpPut("series/{id:int}")]
    [Authorize(Policy = LeavePolicies.LeaveEditOrUser)]
    [ProducesResponseType(typeof(SchedulingLeaveSeriesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SchedulingLeaveSeriesResponse>> UpdateLeaveSeries(
        int id,
        [FromBody] SchedulingLeaveSeriesRequest request,
        CancellationToken cancellationToken
    )
    {
        await leaveSeriesRequestValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await leaveService.UpdateLeaveSeriesAsync(id, request, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("series/{id:int}")]
    [Authorize(Policy = LeavePolicies.LeaveDeleteOrUser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteLeaveSeries(int id, CancellationToken cancellationToken) =>
        await leaveService.DeleteLeaveSeriesAsync(id, cancellationToken) ? NoContent() : NotFound();

    [HttpGet("entries/{id:int}")]
    [Authorize(Policy = LeavePolicies.LeaveViewOrUser)]
    [ProducesResponseType(typeof(SchedulingLeaveEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SchedulingLeaveEntryResponse>> GetLeaveEntryById(
        int id,
        CancellationToken cancellationToken
    )
    {
        var result = await leaveService.GetLeaveEntryByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("entries")]
    [Authorize(Policy = LeavePolicies.LeaveCreateOrUser)]
    [ProducesResponseType(typeof(SchedulingLeaveEntryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SchedulingLeaveEntryResponse>> CreateLeaveEntry(
        [FromBody] SchedulingLeaveEntryRequest request,
        CancellationToken cancellationToken
    )
    {
        await leaveEntryRequestValidator.ValidateAndThrowAsync(request, cancellationToken);

        var result = await leaveService.CreateLeaveEntryAsync(request, cancellationToken);
        return Created($"/api/scheduling/leave/entries/{result.Id}", result);
    }

    [HttpPut("entries/{id:int}")]
    [Authorize(Policy = LeavePolicies.LeaveEditOrUser)]
    [ProducesResponseType(typeof(SchedulingLeaveEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SchedulingLeaveEntryResponse>> UpdateLeaveEntry(
        int id,
        [FromBody] SchedulingLeaveEntryRequest request,
        CancellationToken cancellationToken
    )
    {
        await leaveEntryRequestValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await leaveService.UpdateLeaveEntryAsync(id, request, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("entries/{id:int}")]
    [Authorize(Policy = LeavePolicies.LeaveDeleteOrUser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteLeaveEntry(int id, CancellationToken cancellationToken) =>
        await leaveService.DeleteLeaveEntryAsync(id, cancellationToken) ? NoContent() : NotFound();
}
