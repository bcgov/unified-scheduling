using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Unified.JCInterface.Services;

namespace Unified.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class JCInterfaceController : ControllerBase
{
    private readonly ILogger<JCInterfaceController> _logger;
    private readonly JCDataUpdaterService _jcDataUpdaterService;

    public JCInterfaceController(ILogger<JCInterfaceController> logger, JCDataUpdaterService jcDataUpdaterService)
    {
        _logger = logger;
        _jcDataUpdaterService = jcDataUpdaterService;
    }

    [HttpPost("sync-regions")]
    [AllowAnonymous]
    public async Task<ActionResult> SyncRegions()
    {
        await _jcDataUpdaterService.SyncRegionsAsync();
        return Ok();
    }

    [HttpPost("sync-locations")]
    [AllowAnonymous]
    public async Task<ActionResult> SyncLocations()
    {
        await _jcDataUpdaterService.SyncLocationsAsync();
        return Ok();
    }

    [HttpPost("sync-court-rooms")]
    [AllowAnonymous]
    public async Task<ActionResult> SyncCourtRooms()
    {
        await _jcDataUpdaterService.SyncCourtRoomsAsync();
        return Ok();
    }
}
