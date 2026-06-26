using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Unified.Authorization;
using Unified.Authorization.Claims;
using Unified.Stats.Models;
using Unified.Stats.Services;

namespace Unified.Stats.Controllers;

[ApiController]
[Route("api/stats/dashboard")]
[Authorize(Policy = AuthorizationModule.PolicyPrefix + nameof(Permissions.DashboardView))]
public class DashboardController(IDashboardService service) : ControllerBase
{
    [HttpGet("entries")]
    [ProducesResponseType(typeof(IEnumerable<DashboardEntryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<DashboardEntryResponse>>> GetEntries(
        [FromQuery] DashboardEntriesQueryParams? queryParams,
        CancellationToken cancellationToken
    )
    {
        if (!TryGetHomeLocationId(out var homeLocationId))
            return Forbid();

        return Ok(await service.GetEntriesAsync(homeLocationId, queryParams, cancellationToken));
    }

    [HttpGet("summary")]
    [ProducesResponseType(typeof(DashboardSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DashboardSummaryResponse>> GetSummary(
        [FromQuery] DashboardEntriesQueryParams? queryParams,
        CancellationToken cancellationToken
    )
    {
        if (!TryGetHomeLocationId(out var homeLocationId))
            return Forbid();

        return Ok(await service.GetSummaryAsync(homeLocationId, queryParams, cancellationToken));
    }

    [HttpPost("sign-off")]
    [Authorize(Policy = AuthorizationModule.PolicyPrefix + nameof(Permissions.DashboardSignOff))]
    [ProducesResponseType(typeof(DashboardSignOffResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DashboardSignOffResponse>> SignOff(
        [FromBody] DashboardSignOffRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!TryGetHomeLocationId(out var homeLocationId))
            return Forbid();

        if (!TryGetCallerId(out var callerId))
            return Forbid();

        return Ok(await service.SignOffAsync(homeLocationId, callerId, request, cancellationToken));
    }

    private bool TryGetHomeLocationId(out int homeLocationId)
    {
        var value = User.FindFirst(UnifiedClaimTypes.HomeLocationId)?.Value;
        return int.TryParse(value, out homeLocationId);
    }

    private bool TryGetCallerId(out Guid callerId)
    {
        var value = User.FindFirst(UnifiedClaimTypes.UserId)?.Value;
        return Guid.TryParse(value, out callerId);
    }
}
