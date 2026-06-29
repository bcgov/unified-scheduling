using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Unified.UserManagement.Models;
using Unified.UserManagement.Services;
using Unified.UserManagement.Validators;

namespace Unified.UserManagement.Controllers;

[ApiController]
[Route("api/users/{userId:guid}/acting-positions")]
[Authorize]
public class ActingPositionsController(
    IActingPositionService actingPositionService,
    ActingPositionRequestValidator actingPositionRequestValidator,
    ExpireActingPositionRequestValidator expireActingPositionRequestValidator
) : ControllerBase
{
    /// <summary>
    /// Returns all acting positions for a user.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of acting positions.</returns>
    [HttpGet]
    [Authorize(Policy = UserManagementPolicies.ActingPositionsView)]
    [ProducesResponseType(typeof(IEnumerable<ActingPositionResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<ActingPositionResponseDto>>> GetAll(
        Guid userId,
        CancellationToken cancellationToken
    )
    {
        var positions = await actingPositionService.GetByUserIdAsync(userId, cancellationToken);
        return Ok(positions);
    }

    /// <summary>
    /// Creates a new acting position for a user.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="request">The acting position payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The newly created acting position.</returns>
    [HttpPost]
    [Authorize(Policy = UserManagementPolicies.ActingPositionsCreate)]
    [ProducesResponseType(typeof(ActingPositionResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ActingPositionResponseDto>> Create(
        Guid userId,
        [FromBody] ActingPositionRequestDto request,
        CancellationToken cancellationToken
    )
    {
        await actingPositionRequestValidator.ValidateAndThrowAsync(request, cancellationToken);

        var position = await actingPositionService.CreateAsync(userId, request, cancellationToken);

        return Created($"/api/users/{userId}/acting-positions/{position.Id}", position);
    }

    /// <summary>
    /// Updates an existing acting position for a user.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="actingPositionId">The acting position identifier.</param>
    /// <param name="request">The acting position payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated acting position.</returns>
    [HttpPut("{actingPositionId:int}")]
    [Authorize(Policy = UserManagementPolicies.ActingPositionsEdit)]
    [ProducesResponseType(typeof(ActingPositionResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ActingPositionResponseDto>> Update(
        Guid userId,
        int actingPositionId,
        [FromBody] ActingPositionRequestDto request,
        CancellationToken cancellationToken
    )
    {
        await actingPositionRequestValidator.ValidateAndThrowAsync(request, cancellationToken);

        var position = await actingPositionService.UpdateAsync(userId, actingPositionId, request, cancellationToken);

        return Ok(position);
    }

    /// <summary>
    /// Expires an acting position for a user.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="request">The expire payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The expired acting position.</returns>
    [HttpPost("expire")]
    [Authorize(Policy = UserManagementPolicies.ActingPositionsExpire)]
    [ProducesResponseType(typeof(ActingPositionResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ActingPositionResponseDto>> Expire(
        Guid userId,
        [FromBody] ExpireActingPositionRequestDto request,
        CancellationToken cancellationToken
    )
    {
        await expireActingPositionRequestValidator.ValidateAndThrowAsync(request, cancellationToken);

        var position = await actingPositionService.ExpireAsync(userId, request, cancellationToken);

        return Ok(position);
    }
}
