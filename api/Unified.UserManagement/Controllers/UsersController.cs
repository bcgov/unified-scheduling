using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Unified.UserManagement.Models;
using Unified.UserManagement.Services;
using Unified.UserManagement.Validators;

namespace Unified.UserManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController(
    IUserService userService,
    UserRequestValidator userRequestValidator,
    AssignUserRoleRequestValidator assignUserRoleRequestValidator
) : ControllerBase
{
    /// <summary>
    /// Returns users filtered by optional query parameters.
    /// </summary>
    /// <param name="queryParams">Optional filters for user search and status.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of users.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<UserResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<UserResponse>>> Get(
        [FromQuery] UserQueryParams? queryParams,
        CancellationToken cancellationToken
    )
    {
        var users = await userService.GetAllAsync(queryParams, cancellationToken);
        return Ok(users);
    }

    /// <summary>
    /// Returns a user by id.
    /// </summary>
    /// <param name="id">The user identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The requested user if found.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var user = await userService.GetByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        return Ok(user);
    }

    /// <summary>
    /// Returns role assignments for a user.
    /// </summary>
    /// <param name="id">The user identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The list of assigned roles for the user.</returns>
    [HttpGet("{id:guid}/roles")]
    [ProducesResponseType(typeof(IEnumerable<UserRoleResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<UserRoleResponseDto>>> GetRoles(Guid id, CancellationToken cancellationToken)
    {
        var userRoles = await userService.GetRolesAsync(id, cancellationToken);
        return Ok(userRoles);
    }

    /// <summary>
    /// Creates a new user.
    /// </summary>
    /// <param name="request">The user payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The newly created user.</returns>
    [HttpPost]
    [Authorize(Policy = UserManagementPolicies.UsersCreate)]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserResponse>> Create(
        [FromBody] UserRequestDto request,
        CancellationToken cancellationToken
    )
    {
        await userRequestValidator.ValidateAndThrowAsync(request, cancellationToken);

        var user = await userService.CreateAsync(request, cancellationToken);

        return Created($"/api/users/{user.Id}", user);
    }

    /// <summary>
    /// Updates an existing user.
    /// </summary>
    /// <param name="id">The user identifier.</param>
    /// <param name="request">The updated user payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated user if found.</returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponse>> Update(
        Guid id,
        [FromBody] UserRequestDto request,
        CancellationToken cancellationToken
    )
    {
        await userRequestValidator.ValidateAndThrowAsync(request, cancellationToken);

        var user = await userService.UpdateAsync(id, request, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        return Ok(user);
    }

    /// <summary>
    /// Assigns a role to a user.
    /// </summary>
    /// <param name="id">The user identifier.</param>
    /// <param name="request">The role assignment payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The assigned user role if the user exists.</returns>
    [HttpPost("{id:guid}/roles")]
    [Authorize(Policy = UserManagementPolicies.UserRoleAssign)]
    [ProducesResponseType(typeof(UserRoleResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserRoleResponseDto>> AssignRole(
        Guid id,
        [FromBody] AssignUserRoleRequestDto request,
        CancellationToken cancellationToken
    )
    {
        await assignUserRoleRequestValidator.ValidateAndThrowAsync(request, cancellationToken);

        var userRole = await userService.AssignRoleAsync(id, request, cancellationToken);
        return Ok(userRole);
    }
}
