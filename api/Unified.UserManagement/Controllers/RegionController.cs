using System.Collections.Generic;
using System.Threading.Tasks;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Unified.UserManagement.Models;
using Unified.Db;

namespace Unified.UserManagement.Controllers;

[Route("api/[controller]")]
[Authorize]
[ApiController]
public class RegionController(UnifiedDbContext Db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<RegionDto>>> Regions()
    {
        var regions = await Db.Regions.ToListAsync();
        return Ok(regions.Adapt<List<RegionDto>>());
    }
}
