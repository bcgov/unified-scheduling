using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Unified.Leave;
using Unified.Leave.FeatureFlags;
using Unified.Scheduling;
using Unified.Scheduling.Models;
using Unified.Scheduling.Services;
using Unified.Scheduling.Validators;

namespace Unified.Scheduling.Controllers;

[ApiController]
[Route("api/scheduling/working-hours")]
public class WorkingHoursController : ControllerBase
{
    private readonly IWorkingHoursService _workingHoursQueryService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IOptions<LeaveFeatureFlags> _leaveOptions;
    private readonly WorkingHoursQueryValidator _validator;

    public WorkingHoursController(
        IWorkingHoursService workingHoursQueryService,
        IAuthorizationService authorizationService,
        IOptions<LeaveFeatureFlags> leaveOptions,
        WorkingHoursQueryValidator validator
    )
    {
        _workingHoursQueryService = workingHoursQueryService;
        _authorizationService = authorizationService;
        _leaveOptions = leaveOptions;
        _validator = validator;
    }

    [HttpPost("query")]
    [Authorize(Policy = SchedulingPolicies.AssignmentsView)]
    [Authorize(Policy = SchedulingPolicies.ShiftsView)]
    [ProducesResponseType<IReadOnlyCollection<WorkingHoursResult>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<WorkingHoursResult>>> Query(
        [FromBody] WorkingHoursQuery query,
        CancellationToken cancellationToken
    )
    {
        if (
            _leaveOptions.Value.Enabled
            && !(await _authorizationService.AuthorizeAsync(User, LeavePolicies.LeaveView)).Succeeded
        )
        {
            return Forbid();
        }

        await _validator.ValidateAndThrowAsync(query, cancellationToken);
        var results = await _workingHoursQueryService.QueryAsync(query, cancellationToken);

        return Ok(results);
    }
}
