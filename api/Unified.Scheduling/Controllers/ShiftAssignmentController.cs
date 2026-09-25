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
    IProposedShiftAssignmentOptionsService proposedShiftAssignmentOptionsService,
    ShiftAssignmentEntryRequestValidator entryRequestValidator,
    ShiftAssignmentSeriesRequestValidator seriesRequestValidator,
    ShiftAssignmentEntryUpdateRequestValidator entryUpdateRequestValidator,
    ShiftAssignmentSeriesUpdateRequestValidator seriesUpdateRequestValidator,
    ProposedShiftAssignmentOptionsRequestValidator optionsRequestValidator
) : ControllerBase
{
    [HttpPost("options")]
    [Authorize(Policy = SchedulingPolicies.AssignmentsView)]
    [ProducesResponseType(typeof(ProposedShiftAssignmentOptionsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProposedShiftAssignmentOptionsResponse>> GetOptions(
        [FromBody] ProposedShiftAssignmentOptionsRequest request,
        CancellationToken cancellationToken
    )
    {
        await optionsRequestValidator.ValidateAndThrowAsync(request, cancellationToken);
        return Ok(await proposedShiftAssignmentOptionsService.GetOptionsAsync(request, cancellationToken));
    }

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
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPut("entries/{id:int}")]
    [Authorize(Policy = SchedulingPolicies.AssignmentsAssign)]
    [ProducesResponseType(typeof(ShiftAssignmentEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShiftAssignmentEntryResponse>> UpdateShiftEntryLink(
        int id,
        [FromBody] ShiftAssignmentEntryUpdateRequest request,
        CancellationToken cancellationToken
    )
    {
        await entryUpdateRequestValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await shiftAssignmentService.UpdateShiftEntryLinkAsync(id, request, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("entries/{id:int}")]
    [Authorize(Policy = SchedulingPolicies.AssignmentsAssign)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteShiftEntryLink(int id, CancellationToken cancellationToken) =>
        await shiftAssignmentService.DeleteShiftEntryLinkAsync(id, cancellationToken) ? NoContent() : NotFound();

    [HttpPost("series")]
    [Authorize(Policy = SchedulingPolicies.AssignmentsAssign)]
    [ProducesResponseType(typeof(ShiftAssignmentSeriesLinkResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ShiftAssignmentSeriesLinkResponse>> LinkShiftSeries(
        [FromBody] ShiftAssignmentSeriesRequest request,
        CancellationToken cancellationToken
    )
    {
        await seriesRequestValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await shiftAssignmentService.LinkShiftSeriesAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPut("series/{id:int}")]
    [Authorize(Policy = SchedulingPolicies.AssignmentsAssign)]
    [ProducesResponseType(typeof(ShiftAssignmentSeriesLinkResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShiftAssignmentSeriesLinkResponse>> UpdateShiftSeriesLink(
        int id,
        [FromBody] ShiftAssignmentSeriesUpdateRequest request,
        CancellationToken cancellationToken
    )
    {
        await seriesUpdateRequestValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await shiftAssignmentService.UpdateShiftSeriesLinkAsync(id, request, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("series/{id:int}")]
    [Authorize(Policy = SchedulingPolicies.AssignmentsAssign)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteShiftSeriesLink(int id, CancellationToken cancellationToken) =>
        await shiftAssignmentService.DeleteShiftSeriesLinkAsync(id, cancellationToken) ? NoContent() : NotFound();
}
