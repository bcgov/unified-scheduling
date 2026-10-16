using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
    [Authorize(Policy = SchedulingPolicies.SchedulePublish)]
    [ProducesResponseType(typeof(SchedulePublishPreviewResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SchedulePublishPreviewResponse>> Preview(
        [FromBody] SchedulePublishRequest request,
        CancellationToken cancellationToken
    )
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        return Ok(await schedulePublishService.PreviewAsync(request, cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = SchedulingPolicies.SchedulePublish)]
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
            return Conflict(exception.Preview);
        }
    }
}
