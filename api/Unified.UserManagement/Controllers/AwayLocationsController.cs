using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Unified.UserManagement.Models;
using Unified.UserManagement.Services;
using Unified.UserManagement.Validators;

namespace Unified.UserManagement.Controllers;

[ApiController]
[Route("api/users/{userId:guid}/away-locations")]
[Authorize]
public class AwayLocationsController(
    IAwayLocationService awayLocationService,
    AwayLocationRequestValidator awayLocationRequestValidator,
    ExpireAwayLocationRequestValidator expireAwayLocationRequestValidator
) : ControllerBase
{
    /// <summary>
    /// Returns all away locations for a user.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of away locations.</returns>
    [HttpGet]
    [Authorize(Policy = UserManagementPolicies.AwayLocationsView)]
    [ProducesResponseType(typeof(IEnumerable<AwayLocationResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<AwayLocationResponseDto>>> GetAll(
        Guid userId,
        CancellationToken cancellationToken
    )
    {
        var awayLocations = await awayLocationService.GetByUserIdAsync(userId, cancellationToken);
        return Ok(awayLocations);
    }

    /// <summary>
    /// Creates a new away location for a user.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="request">The away location payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The newly created away location.</returns>
    [HttpPost]
    [Authorize(Policy = UserManagementPolicies.AwayLocationsCreate)]
    [ProducesResponseType(typeof(AwayLocationResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AwayLocationResponseDto>> Create(
        Guid userId,
        [FromBody] AwayLocationRequestDto request,
        CancellationToken cancellationToken
    )
    {
        await awayLocationRequestValidator.ValidateAndThrowAsync(request, cancellationToken);

        var awayLocation = await awayLocationService.CreateAsync(userId, request, cancellationToken);

        return Created($"/api/users/{userId}/away-locations/{awayLocation.Id}", awayLocation);
    }

    /// <summary>
    /// Updates an existing away location for a user.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="awayLocationId">The away location identifier.</param>
    /// <param name="request">The away location payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated away location.</returns>
    [HttpPut("{awayLocationId:int}")]
    [Authorize(Policy = UserManagementPolicies.AwayLocationsEdit)]
    [ProducesResponseType(typeof(AwayLocationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AwayLocationResponseDto>> Update(
        Guid userId,
        int awayLocationId,
        [FromBody] AwayLocationRequestDto request,
        CancellationToken cancellationToken
    )
    {
        await awayLocationRequestValidator.ValidateAndThrowAsync(request, cancellationToken);

        var awayLocation = await awayLocationService.UpdateAsync(userId, awayLocationId, request, cancellationToken);

        return Ok(awayLocation);
    }

    /// <summary>
    /// Expires an away location for a user.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="request">The expire payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The expired away location.</returns>
    [HttpPost("expire")]
    [Authorize(Policy = UserManagementPolicies.AwayLocationsExpire)]
    [ProducesResponseType(typeof(AwayLocationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AwayLocationResponseDto>> Expire(
        Guid userId,
        [FromBody] ExpireAwayLocationRequestDto request,
        CancellationToken cancellationToken
    )
    {
        await expireAwayLocationRequestValidator.ValidateAndThrowAsync(request, cancellationToken);

        var awayLocation = await awayLocationService.ExpireAsync(userId, request, cancellationToken);

        return Ok(awayLocation);
    }
}
