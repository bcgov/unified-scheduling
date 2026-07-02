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
[Route("api/scheduling")]
public sealed class ShiftController(
    IShiftService shiftService,
    ShiftSeriesRequestValidator shiftSeriesRequestValidator,
    ShiftEntryRequestValidator shiftEntryRequestValidator
) : ControllerBase
{
    [HttpGet("shift-series")]
    [Authorize(Policy = SchedulingPolicies.ShiftsView)]
    [ProducesResponseType(typeof(IEnumerable<ShiftSeriesResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ShiftSeriesResponse>>> GetShiftSeries(
        [FromQuery] ShiftSeriesQueryParams? queryParams,
        CancellationToken cancellationToken
    )
    {
        return Ok(await shiftService.GetShiftSeriesAsync(queryParams, cancellationToken));
    }

    [HttpGet("shift-series/{id:int}")]
    [Authorize(Policy = SchedulingPolicies.ShiftsView)]
    [ProducesResponseType(typeof(ShiftSeriesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShiftSeriesResponse>> GetShiftSeriesById(int id, CancellationToken cancellationToken)
    {
        var result = await shiftService.GetShiftSeriesByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("shift-series")]
    [Authorize(Policy = SchedulingPolicies.ShiftsCreateAndAssign)]
    [ProducesResponseType(typeof(ShiftSeriesResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ShiftSeriesResponse>> CreateShiftSeries(
        [FromBody] ShiftSeriesRequest request,
        CancellationToken cancellationToken
    )
    {
        await shiftSeriesRequestValidator.ValidateAndThrowAsync(request, cancellationToken);

        var result = await shiftService.CreateShiftSeriesAsync(request, cancellationToken);
        return Created($"/api/scheduling/shift-series/{result.Id}", result);
    }

    [HttpPut("shift-series/{id:int}")]
    [Authorize(Policy = SchedulingPolicies.ShiftsEdit)]
    [ProducesResponseType(typeof(ShiftSeriesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShiftSeriesResponse>> UpdateShiftSeries(
        int id,
        [FromBody] ShiftSeriesRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!string.IsNullOrWhiteSpace(request.StatusTypeCode))
            return BadRequest("Shift status changes must use the publish or expire endpoints.");

        await shiftSeriesRequestValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await shiftService.UpdateShiftSeriesAsync(id, request, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("shift-series/{id:int}/publish")]
    [Authorize(Policy = SchedulingPolicies.ShiftsEdit)]
    [ProducesResponseType(typeof(ShiftSeriesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShiftSeriesResponse>> PublishShiftSeries(int id, CancellationToken cancellationToken)
    {
        var result = await shiftService.PublishShiftSeriesAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("shift-series/{id:int}/expire")]
    [Authorize(Policy = SchedulingPolicies.ShiftsExpire)]
    [ProducesResponseType(typeof(ShiftSeriesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShiftSeriesResponse>> ExpireShiftSeries(
        int id,
        [FromBody] ExpireShiftRequest? request,
        CancellationToken cancellationToken
    )
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
            return Forbid();

        var result = await shiftService.ExpireShiftSeriesAsync(
            id,
            request ?? new ExpireShiftRequest(),
            currentUserId,
            cancellationToken
        );
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("shift-series/{id:int}")]
    [Authorize(Policy = SchedulingPolicies.ShiftsEdit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteShiftSeries(int id, CancellationToken cancellationToken)
    {
        var deleted = await shiftService.DeleteShiftSeriesAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    [HttpGet("shift-entries")]
    [Authorize(Policy = SchedulingPolicies.ShiftsView)]
    [ProducesResponseType(typeof(IEnumerable<ShiftEntryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ShiftEntryResponse>>> GetShiftEntries(
        [FromQuery] ShiftEntryQueryParams? queryParams,
        CancellationToken cancellationToken
    )
    {
        return Ok(await shiftService.GetShiftEntriesAsync(queryParams, cancellationToken));
    }

    [HttpGet("shift-entries/{id:int}")]
    [Authorize(Policy = SchedulingPolicies.ShiftsView)]
    [ProducesResponseType(typeof(ShiftEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShiftEntryResponse>> GetShiftEntryById(int id, CancellationToken cancellationToken)
    {
        var result = await shiftService.GetShiftEntryByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("shift-entries")]
    [Authorize(Policy = SchedulingPolicies.ShiftsCreateAndAssign)]
    [ProducesResponseType(typeof(ShiftEntryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ShiftEntryResponse>> CreateShiftEntry(
        [FromBody] ShiftEntryRequest request,
        CancellationToken cancellationToken
    )
    {
        await shiftEntryRequestValidator.ValidateAndThrowAsync(request, cancellationToken);

        var result = await shiftService.CreateShiftEntryAsync(request, cancellationToken);
        return Created($"/api/scheduling/shift-entries/{result.Id}", result);
    }

    [HttpPut("shift-entries/{id:int}")]
    [Authorize(Policy = SchedulingPolicies.ShiftsEdit)]
    [ProducesResponseType(typeof(ShiftEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShiftEntryResponse>> UpdateShiftEntry(
        int id,
        [FromBody] ShiftEntryRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!string.IsNullOrWhiteSpace(request.StatusTypeCode))
            return BadRequest("Shift status changes must use the publish or expire endpoints.");

        await shiftEntryRequestValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await shiftService.UpdateShiftEntryAsync(id, request, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("shift-entries/{id:int}/publish")]
    [Authorize(Policy = SchedulingPolicies.ShiftsEdit)]
    [ProducesResponseType(typeof(ShiftEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShiftEntryResponse>> PublishShiftEntry(int id, CancellationToken cancellationToken)
    {
        var result = await shiftService.PublishShiftEntryAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("shift-entries/{id:int}/expire")]
    [Authorize(Policy = SchedulingPolicies.ShiftsExpire)]
    [ProducesResponseType(typeof(ShiftEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShiftEntryResponse>> ExpireShiftEntry(
        int id,
        [FromBody] ExpireShiftRequest? request,
        CancellationToken cancellationToken
    )
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
            return Forbid();

        var result = await shiftService.ExpireShiftEntryAsync(
            id,
            request ?? new ExpireShiftRequest(),
            currentUserId,
            cancellationToken
        );
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("shift-entries/{id:int}")]
    [Authorize(Policy = SchedulingPolicies.ShiftsEdit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteShiftEntry(int id, CancellationToken cancellationToken)
    {
        var deleted = await shiftService.DeleteShiftEntryAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    private Guid? GetCurrentUserId()
    {
        var userIdValue = User.FindFirst(UnifiedClaimTypes.UserId)?.Value;
        return Guid.TryParse(userIdValue, out var userId) ? userId : null;
    }
}
