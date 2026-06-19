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
        var locationIdValue = User.FindFirst(UnifiedClaimTypes.HomeLocationId)?.Value;
        if (!int.TryParse(locationIdValue, out var homeLocationId))
            return Forbid();

        var effectiveLocationId = queryParams?.LocationId ?? homeLocationId;

        return Ok(await service.GetEntriesAsync(effectiveLocationId, queryParams, cancellationToken));
    }
}
