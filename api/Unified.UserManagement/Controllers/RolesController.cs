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
public class RolesController(
    IRoleService roleService,
    RoleRequestValidator roleRequestValidator,
    UpdateRoleRequestValidator updateRoleRequestValidator
) : ControllerBase
{
    /// <summary>
    /// Returns all available roles with their assigned permissions.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of roles.</returns>
    [HttpGet]
    [Authorize(Policy = UserManagementPolicies.RolesView)]
    [ProducesResponseType(typeof(IEnumerable<RoleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<RoleDto>>> Get(CancellationToken cancellationToken)
    {
        var roles = await roleService.GetAllAsync(cancellationToken);
        return Ok(roles);
    }

    /// <summary>
    /// Creates a new role, optionally assigning permissions.
    /// </summary>
    /// <param name="request">The role payload including optional permission IDs.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The newly created role.</returns>
    [HttpPost]
    [Authorize(Policy = UserManagementPolicies.RolesCreate)]
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

    /// <summary>
    /// Updates an existing role's name, description, and permission assignments.
    /// Permissions not included in the request are removed from the role.
    /// </summary>
    /// <param name="id">The role ID.</param>
    /// <param name="request">The updated role payload including the full desired permission set.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated role.</returns>
    [HttpPut("{id:int}")]
    [Authorize(Policy = UserManagementPolicies.RolesEdit)]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RoleDto>> Update(
        int id,
        [FromBody] UpdateRoleRequestDto request,
        CancellationToken cancellationToken
    )
    {
        if (id != request.Id)
            return BadRequest();

        await updateRoleRequestValidator.ValidateAndThrowAsync(request, cancellationToken);

        var role = await roleService.UpdateAsync(request, cancellationToken);

        return Ok(role);
    }

    /// <summary>
    /// Deletes a role and its related user-role and role-permission assignments.
    /// </summary>
    /// <param name="id">The role ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content.</returns>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = UserManagementPolicies.RolesExpire)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await roleService.DeleteAsync(id, cancellationToken);

        return NoContent();
    }
}
