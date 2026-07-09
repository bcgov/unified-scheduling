using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Unified.Authorization.Claims;
using Unified.Scheduling.Models;
using Unified.Scheduling.Services;
using Unified.Scheduling.Validators;

namespace Unified.Scheduling.Controllers;

[ApiController]
[Authorize]
[Route("api/scheduling/assignments")]
public sealed class AssignmentController(
    IAssignmentService assignmentService,
    AssignmentSeriesRequestValidator assignmentSeriesRequestValidator,
    AssignmentEntryRequestValidator assignmentEntryRequestValidator
) : ControllerBase
{
    [HttpGet("series")]
    [Authorize(Policy = SchedulingPolicies.AssignmentsView)]
    [ProducesResponseType(typeof(IEnumerable<AssignmentSeriesResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AssignmentSeriesResponse>>> GetAssignmentSeries(
        [FromQuery] AssignmentSeriesQueryParams? queryParams,
        CancellationToken cancellationToken
    ) => Ok(await assignmentService.GetAssignmentSeriesAsync(queryParams, cancellationToken));

    [HttpGet("series/{id:int}")]
    [Authorize(Policy = SchedulingPolicies.AssignmentsView)]
    [ProducesResponseType(typeof(AssignmentSeriesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AssignmentSeriesResponse>> GetAssignmentSeriesById(
        int id,
        CancellationToken cancellationToken
    )
    {
        var result = await assignmentService.GetAssignmentSeriesByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("series")]
    [Authorize(Policy = SchedulingPolicies.AssignmentsCreate)]
    [ProducesResponseType(typeof(AssignmentSeriesResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AssignmentSeriesResponse>> CreateAssignmentSeries(
        [FromBody] AssignmentSeriesRequest request,
        CancellationToken cancellationToken
    )
    {
        await assignmentSeriesRequestValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await assignmentService.CreateAssignmentSeriesAsync(request, cancellationToken);
        return Created($"/api/scheduling/assignments/series/{result.Id}", result);
    }

    [HttpPut("series/{id:int}")]
    [Authorize(Policy = SchedulingPolicies.AssignmentsEdit)]
    [ProducesResponseType(typeof(AssignmentSeriesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AssignmentSeriesResponse>> UpdateAssignmentSeries(
        int id,
        [FromBody] AssignmentSeriesRequest request,
        CancellationToken cancellationToken
    )
    {
        await assignmentSeriesRequestValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await assignmentService.UpdateAssignmentSeriesAsync(id, request, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("series/{id:int}/expire")]
    [Authorize(Policy = SchedulingPolicies.AssignmentsExpire)]
    [ProducesResponseType(typeof(AssignmentSeriesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AssignmentSeriesResponse>> ExpireAssignmentSeries(
        int id,
        [FromBody] ExpireShiftRequest? request,
        CancellationToken cancellationToken
    )
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
            return Forbid();

        var result = await assignmentService.ExpireAssignmentSeriesAsync(
            id,
            request ?? new ExpireShiftRequest(),
            currentUserId,
            cancellationToken
        );
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("entries")]
    [Authorize(Policy = SchedulingPolicies.AssignmentsView)]
    [ProducesResponseType(typeof(IEnumerable<AssignmentEntryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AssignmentEntryResponse>>> GetAssignmentEntries(
        [FromQuery] AssignmentEntryQueryParams? queryParams,
        CancellationToken cancellationToken
    ) => Ok(await assignmentService.GetAssignmentEntriesAsync(queryParams, cancellationToken));

    [HttpGet("entries/{id:int}")]
    [Authorize(Policy = SchedulingPolicies.AssignmentsView)]
    [ProducesResponseType(typeof(AssignmentEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AssignmentEntryResponse>> GetAssignmentEntryById(
        int id,
        CancellationToken cancellationToken
    )
    {
        var result = await assignmentService.GetAssignmentEntryByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("entries")]
    [Authorize(Policy = SchedulingPolicies.AssignmentsCreate)]
    [ProducesResponseType(typeof(AssignmentEntryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AssignmentEntryResponse>> CreateAssignmentEntry(
        [FromBody] AssignmentEntryRequest request,
        CancellationToken cancellationToken
    )
    {
        await assignmentEntryRequestValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await assignmentService.CreateAssignmentEntryAsync(request, cancellationToken);
        return Created($"/api/scheduling/assignments/entries/{result.Id}", result);
    }

    [HttpPut("entries/{id:int}")]
    [Authorize(Policy = SchedulingPolicies.AssignmentsEdit)]
    [ProducesResponseType(typeof(AssignmentEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AssignmentEntryResponse>> UpdateAssignmentEntry(
        int id,
        [FromBody] AssignmentEntryRequest request,
        CancellationToken cancellationToken
    )
    {
        await assignmentEntryRequestValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await assignmentService.UpdateAssignmentEntryAsync(id, request, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("entries/{id:int}/expire")]
    [Authorize(Policy = SchedulingPolicies.AssignmentsExpire)]
    [ProducesResponseType(typeof(AssignmentEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AssignmentEntryResponse>> ExpireAssignmentEntry(
        int id,
        [FromBody] ExpireShiftRequest? request,
        CancellationToken cancellationToken
    )
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
            return Forbid();

        var result = await assignmentService.ExpireAssignmentEntryAsync(
            id,
            request ?? new ExpireShiftRequest(),
            currentUserId,
            cancellationToken
        );
        return result is null ? NotFound() : Ok(result);
    }

    private Guid? GetCurrentUserId()
    {
        var userIdValue = User.FindFirst(UnifiedClaimTypes.UserId)?.Value;
        return Guid.TryParse(userIdValue, out var userId) ? userId : null;
    }
}
