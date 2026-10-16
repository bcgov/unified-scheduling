using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Unified.TimeOff.Models.Scheduling;
using Unified.TimeOff.Services;
using Unified.TimeOff.Validators;

namespace Unified.TimeOff.Controllers;

[ApiController]
[Authorize]
[Route("api/scheduling/timeoff")]
public sealed class SchedulingTimeOffController(
    ISchedulingTimeOffService timeOffService,
    SchedulingTimeOffRequestValidator timeOffEntryRequestValidator,
    SchedulingTimeOffSeriesRequestValidator timeOffSeriesRequestValidator
) : ControllerBase
{
    [HttpGet("users/{userId:guid}/entries")]
    [Authorize(Policy = TimeOffPolicies.TimeOffViewOrUser)]
    [ProducesResponseType(typeof(IReadOnlyList<SchedulingTimeOffEntryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SchedulingTimeOffEntryResponse>>> GetTimeOffEntriesByUser(
        Guid userId,
        CancellationToken cancellationToken
    ) => Ok(await timeOffService.GetTimeOffEntriesByUserAsync(userId, cancellationToken));

    [HttpGet("users/{userId:guid}/series")]
    [Authorize(Policy = TimeOffPolicies.TimeOffViewOrUser)]
    [ProducesResponseType(typeof(IReadOnlyList<SchedulingTimeOffSeriesResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SchedulingTimeOffSeriesResponse>>> GetTimeOffSeriesByUser(
        Guid userId,
        CancellationToken cancellationToken
    ) => Ok(await timeOffService.GetTimeOffSeriesByUserAsync(userId, cancellationToken));

    [HttpGet("series/{id:int}")]
    [Authorize(Policy = TimeOffPolicies.TimeOffViewOrUser)]
    [ProducesResponseType(typeof(SchedulingTimeOffSeriesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SchedulingTimeOffSeriesResponse>> GetTimeOffSeriesById(
        int id,
        CancellationToken cancellationToken
    )
    {
        var result = await timeOffService.GetTimeOffSeriesByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("series")]
    [Authorize(Policy = TimeOffPolicies.TimeOffCreateOrUser)]
    [ProducesResponseType(typeof(SchedulingTimeOffSeriesResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SchedulingTimeOffSeriesResponse>> CreateTimeOffSeries(
        [FromBody] SchedulingTimeOffSeriesRequest request,
        CancellationToken cancellationToken
    )
    {
        await timeOffSeriesRequestValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await timeOffService.CreateTimeOffSeriesAsync(request, cancellationToken);
        return Created($"/api/scheduling/timeoff/series/{result.Id}", result);
    }

    [HttpPut("series/{id:int}")]
    [Authorize(Policy = TimeOffPolicies.TimeOffEditOrUser)]
    [ProducesResponseType(typeof(SchedulingTimeOffSeriesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SchedulingTimeOffSeriesResponse>> UpdateTimeOffSeries(
        int id,
        [FromBody] SchedulingTimeOffSeriesRequest request,
        CancellationToken cancellationToken
    )
    {
        await timeOffSeriesRequestValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await timeOffService.UpdateTimeOffSeriesAsync(id, request, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("series/{id:int}")]
    [Authorize(Policy = TimeOffPolicies.TimeOffDeleteOrUser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTimeOffSeries(int id, CancellationToken cancellationToken) =>
        await timeOffService.DeleteTimeOffSeriesAsync(id, cancellationToken) ? NoContent() : NotFound();

    [HttpGet("entries/{id:int}")]
    [Authorize(Policy = TimeOffPolicies.TimeOffViewOrUser)]
    [ProducesResponseType(typeof(SchedulingTimeOffEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SchedulingTimeOffEntryResponse>> GetTimeOffEntryById(
        int id,
        CancellationToken cancellationToken
    )
    {
        var result = await timeOffService.GetTimeOffEntryByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("entries")]
    [Authorize(Policy = TimeOffPolicies.TimeOffCreateOrUser)]
    [ProducesResponseType(typeof(SchedulingTimeOffEntryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SchedulingTimeOffEntryResponse>> CreateTimeOffEntry(
        [FromBody] SchedulingTimeOffEntryRequest request,
        CancellationToken cancellationToken
    )
    {
        await timeOffEntryRequestValidator.ValidateAndThrowAsync(request, cancellationToken);

        var result = await timeOffService.CreateTimeOffEntryAsync(request, cancellationToken);
        return Created($"/api/scheduling/timeoff/entries/{result.Id}", result);
    }

    [HttpPut("entries/{id:int}")]
    [Authorize(Policy = TimeOffPolicies.TimeOffEditOrUser)]
    [ProducesResponseType(typeof(SchedulingTimeOffEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SchedulingTimeOffEntryResponse>> UpdateTimeOffEntry(
        int id,
        [FromBody] SchedulingTimeOffEntryRequest request,
        CancellationToken cancellationToken
    )
    {
        await timeOffEntryRequestValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await timeOffService.UpdateTimeOffEntryAsync(id, request, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("entries/{id:int}")]
    [Authorize(Policy = TimeOffPolicies.TimeOffDeleteOrUser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTimeOffEntry(int id, CancellationToken cancellationToken) =>
        await timeOffService.DeleteTimeOffEntryAsync(id, cancellationToken) ? NoContent() : NotFound();
}
