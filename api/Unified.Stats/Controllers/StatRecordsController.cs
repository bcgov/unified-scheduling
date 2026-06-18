using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Unified.Authorization;
using Unified.Authorization.Claims;
using Unified.Stats.Models;
using Unified.Stats.Services;
using Unified.Stats.Validators;

namespace Unified.Stats.Controllers;

[ApiController]
[Route("api/stats/records")]
[Authorize]
public class StatRecordsController(IStatRecordService service, StatRecordRequestValidator validator) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<StatRecordResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<StatRecordResponse>>> GetAll(
        [FromQuery] StatRecordQueryParams? queryParams,
        CancellationToken cancellationToken
    )
    {
        if (!TryGetCallerContext(out var callerUserId, out var callerCanEnterForOthers))
            return Unauthorized();

        // Non-supervisors may only query their own records regardless of what UserId is passed
        if (!callerCanEnterForOthers)
            queryParams = (queryParams ?? new()) with { UserId = callerUserId };

        return Ok(await service.GetAllAsync(queryParams, cancellationToken));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(StatRecordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StatRecordResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await service.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(StatRecordResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<StatRecordResponse>> Create(
        [FromBody] StatRecordRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!TryGetCallerContext(out var callerUserId, out var callerCanEnterForOthers))
            return Unauthorized();

        await validator.ValidateAndThrowAsync(request, cancellationToken);

        var result = await service.CreateAsync(request, callerUserId, callerCanEnterForOthers, cancellationToken);
        return Created($"/api/stats/records/{result.Id}", result);
    }

    [HttpPost("batch")]
    [ProducesResponseType(typeof(IEnumerable<StatRecordResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<StatRecordResponse>>> CreateBatch(
        [FromBody] IReadOnlyList<StatRecordRequest> requests,
        CancellationToken cancellationToken
    )
    {
        if (!TryGetCallerContext(out var callerUserId, out var callerCanEnterForOthers))
            return Unauthorized();

        foreach (var request in requests)
            await validator.ValidateAndThrowAsync(request, cancellationToken);

        var result = await service.CreateBatchAsync(requests, callerUserId, callerCanEnterForOthers, cancellationToken);
        return Created("/api/stats/records", result);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(StatRecordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StatRecordResponse>> Update(
        int id,
        [FromBody] StatRecordRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!TryGetCallerContext(out var callerUserId, out var callerCanEnterForOthers))
            return Unauthorized();

        await validator.ValidateAndThrowAsync(request, cancellationToken);

        var result = await service.UpdateAsync(id, request, callerUserId, callerCanEnterForOthers, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("day")]
    [ProducesResponseType(typeof(IEnumerable<StatRecordResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<StatRecordResponse>>> SaveDay(
        [FromBody] SaveDayRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!TryGetCallerContext(out var callerUserId, out var callerCanEnterForOthers))
            return Unauthorized();

        var result = await service.SaveDayAsync(request, callerUserId, callerCanEnterForOthers, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        if (!TryGetCallerContext(out var callerUserId, out var callerCanEnterForOthers))
            return Unauthorized();

        var deleted = await service.DeleteAsync(id, callerUserId, callerCanEnterForOthers, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    /// <summary>
    /// Resolves the caller's DB user ID and whether they hold the
    /// <see cref="Permissions.StatsRecordsEnterForOthers"/> permission from claims.
    /// Returns false if the user ID claim is absent (user not found in DB).
    /// </summary>
    private bool TryGetCallerContext(out Guid callerUserId, out bool callerCanEnterForOthers)
    {
        callerCanEnterForOthers = User.HasClaim(
            UnifiedClaimTypes.Permission,
            Permissions.StatsRecordsEnterForOthers.ToString()
        );

        var userIdValue = User.FindFirst(UnifiedClaimTypes.UserId)?.Value;
        if (Guid.TryParse(userIdValue, out callerUserId))
            return true;

        callerUserId = Guid.Empty;
        return false;
    }
}
