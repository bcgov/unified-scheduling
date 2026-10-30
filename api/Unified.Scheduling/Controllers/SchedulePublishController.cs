using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Unified.Authorization;
using Unified.Authorization.Claims;
using Unified.Scheduling.Models;
using Unified.Scheduling.Services;
using Unified.Scheduling.Validators;

namespace Unified.Scheduling.Controllers;

[ApiController]
[Authorize]
[Route("api/scheduling/publish")]
public sealed class SchedulePublishController(
    ISchedulePublishService schedulePublishService,
    SchedulePublishRequestValidator validator
) : ControllerBase
{
    [HttpPost("preview")]
    [Authorize(Policy = SchedulingPolicies.SchedulePublishWithView)]
    [ProducesResponseType(typeof(SchedulePublishPreviewResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SchedulePublishPreviewResponse>> Preview(
        [FromBody] SchedulePublishRequest request,
        CancellationToken cancellationToken
    )
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        return Ok(VisiblePreview(await schedulePublishService.PreviewAsync(request, cancellationToken)));
    }

    [HttpPost]
    [Authorize(Policy = SchedulingPolicies.SchedulePublishWithView)]
    [ProducesResponseType(typeof(SchedulePublishResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SchedulePublishPreviewResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SchedulePublishResult>> Publish(
        [FromBody] SchedulePublishRequest request,
        CancellationToken cancellationToken
    )
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        try
        {
            return Ok(
                await schedulePublishService.PublishAsync(request, User.TryGetCurrentUserId(), cancellationToken)
            );
        }
        catch (SchedulePublishBlockedException exception)
        {
            return Conflict(VisiblePreview(exception.Preview));
        }
    }

    private SchedulePublishPreviewResponse VisiblePreview(SchedulePublishPreviewResponse preview)
    {
        if (HttpContext?.User.HasClaim(UnifiedClaimTypes.Permission, nameof(Permissions.UserTrainingsView)) == true)
            return preview;

        var conflicts = preview
            .Blockers.Conflicts.Select(
                (conflict, index) =>
                    conflict.FirstSourceModule == "training" || conflict.SecondSourceModule == "training"
                        ? conflict with
                        {
                            Id = $"restricted-conflict:{index}",
                            FirstSourceModule =
                                conflict.FirstSourceModule == "training" ? "restricted" : conflict.FirstSourceModule,
                            FirstEventId =
                                conflict.FirstSourceModule == "training" ? string.Empty : conflict.FirstEventId,
                            SecondSourceModule =
                                conflict.SecondSourceModule == "training" ? "restricted" : conflict.SecondSourceModule,
                            SecondEventId =
                                conflict.SecondSourceModule == "training" ? string.Empty : conflict.SecondEventId,
                        }
                        : conflict
            )
            .ToList();
        return preview with { Blockers = preview.Blockers with { Conflicts = conflicts } };
    }
}
