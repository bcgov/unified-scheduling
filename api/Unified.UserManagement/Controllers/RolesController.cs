using System.Collections.Generic;
using System.Threading.Tasks;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Unified.Db;
using Unified.UserManagement.Models;

namespace Unified.UserManagement.Controllers;

[Route("api/[controller]")]
[Authorize]
[ApiController]
public class RolesController(UnifiedDbContext Db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<RoleDto>>> Roles()
    {
        var roles = await Db.Roles.ToListAsync();
        return Ok(roles.Adapt<List<RoleDto>>());
    }
}
