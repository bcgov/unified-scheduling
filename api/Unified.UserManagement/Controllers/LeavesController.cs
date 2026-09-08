using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Unified.UserManagement.Models;
using Unified.UserManagement.Services;
using Unified.UserManagement.Validators;

namespace Unified.UserManagement.Controllers;

[ApiController]
[Route("api/users/{userId:guid}/leaves")]
[Authorize]
public class LeavesController(
    ILeaveService leaveService,
    LeaveRequestValidator leaveRequestValidator,
    ExpireLeaveRequestValidator expireLeaveRequestValidator
) : ControllerBase
{
    /// <summary>
    /// Returns all leave records for a user.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = UserManagementPolicies.LeaveView)]
    [ProducesResponseType(typeof(IEnumerable<LeaveResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<LeaveResponseDto>>> GetAll(
        Guid userId,
        CancellationToken cancellationToken
    )
    {
        var leaves = await leaveService.GetByUserIdAsync(userId, cancellationToken);
        return Ok(leaves);
    }

    /// <summary>
    /// Creates a new leave record for a user.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = UserManagementPolicies.LeaveCreate)]
    [ProducesResponseType(typeof(LeaveResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeaveResponseDto>> Create(
        Guid userId,
        [FromBody] LeaveRequestDto request,
        CancellationToken cancellationToken
    )
    {
        await leaveRequestValidator.ValidateAndThrowAsync(request, cancellationToken);

        var leave = await leaveService.CreateAsync(userId, request, cancellationToken);

        return Created($"/api/users/{userId}/leaves/{leave.Id}", leave);
    }

    /// <summary>
    /// Updates an existing leave record for a user.
    /// </summary>
    [HttpPut("{leaveId:int}")]
    [Authorize(Policy = UserManagementPolicies.LeaveEdit)]
    [ProducesResponseType(typeof(LeaveResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeaveResponseDto>> Update(
        Guid userId,
        int leaveId,
        [FromBody] LeaveRequestDto request,
        CancellationToken cancellationToken
    )
    {
        await leaveRequestValidator.ValidateAndThrowAsync(request, cancellationToken);

        var leave = await leaveService.UpdateAsync(userId, leaveId, request, cancellationToken);

        return Ok(leave);
    }

    /// <summary>
    /// Expires a leave record for a user.
    /// </summary>
    [HttpPost("expire")]
    [Authorize(Policy = UserManagementPolicies.LeaveExpire)]
    [ProducesResponseType(typeof(LeaveResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeaveResponseDto>> Expire(
        Guid userId,
        [FromBody] ExpireLeaveRequestDto request,
        CancellationToken cancellationToken
    )
    {
        await expireLeaveRequestValidator.ValidateAndThrowAsync(request, cancellationToken);

        var leave = await leaveService.ExpireAsync(userId, request, cancellationToken);

        return Ok(leave);
    }
}
