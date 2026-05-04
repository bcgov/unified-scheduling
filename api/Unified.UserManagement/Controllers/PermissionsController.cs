using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Unified.Authorization;
using Unified.UserManagement.Models;
using Unified.UserManagement.Services;

namespace Unified.UserManagement.Controllers;

[Route("api/[controller]")]
[Authorize]
[ApiController]
public class PermissionsController(IPermissionService permissionService) : ControllerBase
{
    /// <summary>
    /// Returns all permissions.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of permissions.</returns>
    [HttpGet]
    [Authorize(Policy = AuthorizationModule.PolicyPrefix + Permissions.RolesView)]
    [ProducesResponseType(typeof(IEnumerable<PermissionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PermissionDto>>> Get(CancellationToken cancellationToken)
    {
        var permissions = await permissionService.GetAllAsync(cancellationToken);
        return Ok(permissions);
    }
}
