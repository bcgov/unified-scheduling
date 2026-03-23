using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Unified.Core.Models;
using Unified.Db;

namespace Unified.Core.Controllers;

/// <summary>
/// Used to fetch Locations, plus expire locations. These locations are inserted by JCDataUpdaterService.
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class LocationController : ControllerBase
{
    private UnifiedDbContext Db { get; }

    public LocationController(UnifiedDbContext dbContext)
    {
        Db = dbContext;
    }

    [HttpGet]
    [Route("all")]
    public async Task<ActionResult<List<LocationDto>>> AllLocations()
    {
        var locations = await Db.Locations.ToListAsync();
        return Ok(locations.Adapt<List<LocationDto>>());
    }
}
