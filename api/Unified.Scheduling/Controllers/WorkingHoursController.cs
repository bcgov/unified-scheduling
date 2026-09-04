using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Unified.Scheduling;
using Unified.Scheduling.Models;
using Unified.Scheduling.Services;

namespace Unified.Scheduling.Controllers;

[ApiController]
[Route("api/scheduling/working-hours")]
public class WorkingHoursController : ControllerBase
{
    private readonly IWorkingHoursService _workingHoursQueryService;

    public WorkingHoursController(
        IWorkingHoursService workingHoursQueryService)
    {
        _workingHoursQueryService = workingHoursQueryService;
    }

    [HttpPost("query")]
    [Authorize(Policy = SchedulingPolicies.AssignmentsView)]
    [Authorize(Policy = SchedulingPolicies.ShiftsView)]
    [ProducesResponseType<IReadOnlyCollection<WorkingHoursResult>>(
        StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<WorkingHoursResult>>> Query(
        [FromBody] WorkingHoursQuery query,
        CancellationToken cancellationToken)
    {
        var results = await _workingHoursQueryService.QueryAsync(
            query,
            cancellationToken);

        return Ok(results);
    }
}