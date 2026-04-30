using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Unified.UserManagement.Models;
using Unified.UserManagement.Services;
using Unified.UserManagement.Validators;

namespace Unified.UserManagement.Controllers;

[Route("api/[controller]")]
[Authorize]
[ApiController]
public class RolesController(IRoleService roleService, RoleRequestValidator roleRequestValidator) : ControllerBase
{
    /// <summary>
    /// Returns all available roles.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of roles.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<RoleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<RoleDto>>> Get(CancellationToken cancellationToken)
    {
        var roles = await roleService.GetAllAsync(cancellationToken);
        return Ok(roles);
    }

    /// <summary>
    /// Creates a new role.
    /// </summary>
    /// <param name="request">The role payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The newly created role.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RoleDto>> Create(
        [FromBody] RoleRequestDto request,
        CancellationToken cancellationToken
    )
    {
        await roleRequestValidator.ValidateAndThrowAsync(request, cancellationToken);

        var role = await roleService.CreateAsync(request, cancellationToken);

        return Created($"/api/roles/{role.Id}", role);
    }
}
