using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Unified.Authorization;
using Unified.Authorization.Claims;
using Unified.Core.Pdf;
using Unified.Scheduling.Models;
using Unified.Scheduling.Services;
using Unified.Scheduling.Validators;

namespace Unified.Scheduling.Controllers;

[ApiController]
[Authorize]
[Route("api/scheduling/distribute")]
public sealed class ScheduleDistributionController(
    IScheduleDistributionService scheduleDistributionService,
    ScheduleDistributionRequestValidator validator,
    ILogger<ScheduleDistributionController> logger
) : ControllerBase
{
    [HttpPost]
    [HttpLogging(HttpLoggingFields.None)]
    [Authorize(Policy = SchedulingPolicies.ScheduleDistributeWithView)]
    [ProducesResponseType(typeof(ScheduleDistributionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<ScheduleDistributionResult>> Distribute(
        [FromBody] ScheduleDistributionRequest request,
        CancellationToken cancellationToken
    )
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var requiredViewPermission = string.Equals(
            request.Context!.View,
            "assignments",
            StringComparison.OrdinalIgnoreCase
        )
            ? Permissions.AssignmentsView
            : Permissions.ShiftsView;
        if (!User.HasClaim(UnifiedClaimTypes.Permission, requiredViewPermission.ToString()))
            return Forbid();

        try
        {
            return Ok(await scheduleDistributionService.DistributeAsync(request, cancellationToken));
        }
        catch (PdfRenderingException exception)
        {
            logger.LogError(
                "Schedule PDF rendering failed before email submission. Failure category: {FailureCategory}",
                exception.IsServiceUnavailable ? "unavailable" : "rendering-failed"
            );
            return Problem(
                statusCode: exception.IsServiceUnavailable
                    ? StatusCodes.Status503ServiceUnavailable
                    : StatusCodes.Status502BadGateway,
                title: exception.IsServiceUnavailable
                    ? "Schedule distribution is unavailable."
                    : "Schedule rendering failed.",
                detail: exception.IsServiceUnavailable
                    ? "Schedule distribution is not currently configured."
                    : "The schedule PDF could not be generated."
            );
        }
    }
}
