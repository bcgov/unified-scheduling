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
[Route("api/scheduling/shift-assignments")]
public sealed class ShiftAssignmentController(
    IShiftAssignmentService shiftAssignmentService,
    ShiftAssignmentEntryRequestValidator entryRequestValidator,
    ShiftAssignmentSeriesRequestValidator seriesRequestValidator
) : ControllerBase
{
    [HttpPost("entries")]
    [Authorize(Policy = SchedulingPolicies.AssignmentsAssign)]
    [ProducesResponseType(typeof(ShiftAssignmentEntryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ShiftAssignmentEntryResponse>> LinkShiftEntry(
        [FromBody] ShiftAssignmentEntryRequest request,
        CancellationToken cancellationToken
    )
    {
        await entryRequestValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await shiftAssignmentService.LinkShiftEntryAsync(request, cancellationToken);
        return Created($"/api/scheduling/shift-assignments/entries/{result.Id}", result);
    }

    [HttpPost("series")]
    [Authorize(Policy = SchedulingPolicies.AssignmentsAssign)]
    [ProducesResponseType(typeof(IEnumerable<ShiftAssignmentEntryResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<ShiftAssignmentEntryResponse>>> LinkShiftSeries(
        [FromBody] ShiftAssignmentSeriesRequest request,
        CancellationToken cancellationToken
    )
    {
        await seriesRequestValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await shiftAssignmentService.LinkShiftSeriesAsync(request, cancellationToken);
        return Created("/api/scheduling/shift-assignments/series", result);
    }
}
