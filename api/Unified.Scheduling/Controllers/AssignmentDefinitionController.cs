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
[Route("api/scheduling/assignment-definitions")]
public sealed class AssignmentDefinitionController(
    IAssignmentDefinitionService assignmentDefinitionService,
    AssignmentDefinitionRequestValidator validator
) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = SchedulingPolicies.AssignmentsView)]
    [ProducesResponseType(typeof(IEnumerable<AssignmentDefinitionResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AssignmentDefinitionResponse>>> GetAssignmentDefinitions(
        [FromQuery] int? locationId,
        CancellationToken cancellationToken
    ) => Ok(await assignmentDefinitionService.GetAssignmentDefinitionsAsync(locationId, cancellationToken));

    [HttpGet("{id:int}")]
    [Authorize(Policy = SchedulingPolicies.AssignmentsView)]
    [ProducesResponseType(typeof(AssignmentDefinitionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AssignmentDefinitionResponse>> GetAssignmentDefinitionById(
        int id,
        CancellationToken cancellationToken
    )
    {
        var result = await assignmentDefinitionService.GetAssignmentDefinitionByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = SchedulingPolicies.AssignmentsCreate)]
    [ProducesResponseType(typeof(AssignmentDefinitionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AssignmentDefinitionResponse>> CreateAssignmentDefinition(
        [FromBody] AssignmentDefinitionRequest request,
        CancellationToken cancellationToken
    )
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await assignmentDefinitionService.CreateAssignmentDefinitionAsync(request, cancellationToken);
        return Created($"/api/scheduling/assignment-definitions/{result.Id}", result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = SchedulingPolicies.AssignmentsEdit)]
    [ProducesResponseType(typeof(AssignmentDefinitionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AssignmentDefinitionResponse>> UpdateAssignmentDefinition(
        int id,
        [FromBody] AssignmentDefinitionRequest request,
        CancellationToken cancellationToken
    )
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await assignmentDefinitionService.UpdateAssignmentDefinitionAsync(id, request, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
