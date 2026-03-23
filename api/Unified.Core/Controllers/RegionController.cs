using System.Collections.Generic;
using System.Threading.Tasks;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Unified.Core.Models;
using Unified.Db;

namespace Unified.Core.Controllers;

[Route("api/[controller]")]
[Authorize]
[ApiController]
public class RegionController : ControllerBase
{
    private UnifiedDbContext Db { get; }

    public RegionController(UnifiedDbContext dbContext)
    {
        Db = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<List<RegionDto>>> Regions()
    {
        var regions = await Db.Regions.ToListAsync();
        return Ok(regions.Adapt<List<RegionDto>>());
    }
}
