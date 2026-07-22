using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Unified.Authorization.Claims;
using Unified.Training.Models;
using Unified.Training.Services;
using Unified.Training.Validators;

namespace Unified.Training.Controllers;

[ApiController]
[Authorize]
[Route("api/training/user-trainings")]
public sealed class UserTrainingController(
    IUserTrainingService userTrainingService,
    UserTrainingRequestValidator validator
) : ControllerBase
{
    private const string UserTrainingsViewPolicy = TrainingPolicies.UserTrainingsView;
    private const string UserTrainingsCreatePolicy = TrainingPolicies.UserTrainingsCreate;
    private const string UserTrainingsEditPolicy = TrainingPolicies.UserTrainingsEdit;
    private const string UserTrainingsDeletePolicy = TrainingPolicies.UserTrainingsDelete;

    [HttpGet]
    [Authorize(Policy = UserTrainingsViewPolicy)]
    [ProducesResponseType(typeof(IEnumerable<UserTrainingResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyCollection<UserTrainingResponse>>> GetAll(
        [FromQuery] Guid userId,
        CancellationToken cancellationToken
    )
    {
        if (!TryGetCallerUserId(out var callerUserId))
            return Unauthorized();

        var result = await userTrainingService.GetAllAsync(userId, callerUserId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("/api/trainings/users/{userId:guid}")]
    [Authorize(Policy = UserTrainingsViewPolicy)]
    [ProducesResponseType(typeof(IEnumerable<UserTrainingResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyCollection<UserTrainingResponse>>> GetAllByUser(
        Guid userId,
        CancellationToken cancellationToken
    )
    {
        if (!TryGetCallerUserId(out var callerUserId))
            return Unauthorized();

        var result = await userTrainingService.GetAllAsync(userId, callerUserId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("/api/trainings/{trainingId:int}/users/{userId:guid}")]
    [Authorize(Policy = UserTrainingsViewPolicy)]
    [ProducesResponseType(typeof(UserTrainingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserTrainingResponse>> GetByTrainingAndUser(
        int trainingId,
        Guid userId,
        CancellationToken cancellationToken
    )
    {
        if (!TryGetCallerUserId(out var callerUserId))
            return Unauthorized();

        var result = await userTrainingService.GetByTrainingAndUserAsync(
            trainingId,
            userId,
            callerUserId,
            cancellationToken
        );

        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = UserTrainingsCreatePolicy)]
    [ProducesResponseType(typeof(UserTrainingResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UserTrainingResponse>> Create(
        [FromBody] UserTrainingRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!TryGetCallerUserId(out var callerUserId))
            return Unauthorized();

        await validator.ValidateAndThrowAsync(request, cancellationToken);

        var result = await userTrainingService.CreateAsync(request, callerUserId, cancellationToken);

        return Created($"/api/training/user-trainings/{result.Id}", result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = UserTrainingsEditPolicy)]
    [ProducesResponseType(typeof(UserTrainingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserTrainingResponse>> Update(
        int id,
        [FromBody] UserTrainingRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!TryGetCallerUserId(out var callerUserId))
            return Unauthorized();

        await validator.ValidateAndThrowAsync(request, cancellationToken);

        var result = await userTrainingService.UpdateAsync(id, request, callerUserId, cancellationToken);

        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = UserTrainingsDeletePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        if (!TryGetCallerUserId(out var callerUserId))
            return Unauthorized();

        var deleted = await userTrainingService.DeleteAsync(id, callerUserId, cancellationToken);

        return deleted ? NoContent() : NotFound();
    }

    /// <summary>
    /// Resolves the caller's DB user ID from claims.
    /// Returns <c>false</c> if the UserId claim is absent (unrecognised user).
    /// </summary>
    private bool TryGetCallerUserId(out Guid callerUserId)
    {
        var userIdValue = User.FindFirst(UnifiedClaimTypes.UserId)?.Value;
        if (Guid.TryParse(userIdValue, out callerUserId))
            return true;

        callerUserId = Guid.Empty;
        return false;
    }
}
