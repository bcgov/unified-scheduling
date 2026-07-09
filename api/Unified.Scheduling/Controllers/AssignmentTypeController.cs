using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Unified.Scheduling.Models;
using Unified.Scheduling.Services;
using Unified.Scheduling.Validators;

namespace Unified.Scheduling.Controllers;

[ApiController]
[Authorize]
[Route("api/scheduling/assignment-types")]
public sealed class AssignmentTypeController(
    IAssignmentTypeService assignmentTypeService,
    AssignmentTypeRequestValidator validator
) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = SchedulingPolicies.AssignmentTypeRead)]
    [ProducesResponseType(typeof(IEnumerable<AssignmentTypeResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AssignmentTypeResponse>>> GetAssignmentTypes(
        [FromQuery] int? locationId,
        CancellationToken cancellationToken
    ) => Ok(await assignmentTypeService.GetAssignmentTypesAsync(locationId, cancellationToken));

    [HttpGet("{id:int}")]
    [Authorize(Policy = SchedulingPolicies.AssignmentTypeRead)]
    [ProducesResponseType(typeof(AssignmentTypeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AssignmentTypeResponse>> GetAssignmentTypeById(
        int id,
        CancellationToken cancellationToken
    )
    {
        var result = await assignmentTypeService.GetAssignmentTypeByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = SchedulingPolicies.AssignmentTypeWrite)]
    [ProducesResponseType(typeof(AssignmentTypeResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AssignmentTypeResponse>> CreateAssignmentType(
        [FromBody] AssignmentTypeRequest request,
        CancellationToken cancellationToken
    )
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await assignmentTypeService.CreateAssignmentTypeAsync(request, cancellationToken);
        return Created($"/api/scheduling/assignment-types/{result.Id}", result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = SchedulingPolicies.AssignmentTypeWrite)]
    [ProducesResponseType(typeof(AssignmentTypeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AssignmentTypeResponse>> UpdateAssignmentType(
        int id,
        [FromBody] AssignmentTypeRequest request,
        CancellationToken cancellationToken
    )
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await assignmentTypeService.UpdateAssignmentTypeAsync(id, request, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{id:int}/expire")]
    [Authorize(Policy = SchedulingPolicies.AssignmentTypeExpire)]
    [ProducesResponseType(typeof(AssignmentTypeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AssignmentTypeResponse>> ExpireAssignmentType(
        int id,
        CancellationToken cancellationToken
    )
    {
        var result = await assignmentTypeService.ExpireAssignmentTypeAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
