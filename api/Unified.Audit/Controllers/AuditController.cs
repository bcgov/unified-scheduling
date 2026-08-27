using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Unified.Audit.Models;
using Unified.Audit.Services;
using Unified.Audit.Validators;

namespace Unified.Audit.Controllers;

[ApiController]
[Route("api/audit")]
[Authorize(Policy = AuditPolicies.AuditRead)]
public class AuditController(
    IAuditHistoryService auditHistoryService,
    IAuditSchemaService auditSchemaService,
    AuditHistoryQueryParamsValidator auditHistoryQueryParamsValidator
) : ControllerBase
{
    /// <summary>
    /// Returns paginated audit history records filtered by the supplied query parameters.
    /// </summary>
    [HttpGet("history")]
    [ProducesResponseType(typeof(AuditHistoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuditHistoryResponse>> GetHistory(
        [FromQuery] AuditHistoryQueryParams queryParams,
        CancellationToken cancellationToken
    )
    {
        await auditHistoryQueryParamsValidator.ValidateAndThrowAsync(queryParams, cancellationToken);

        if (queryParams.EntityType is { Length: > 0 } entityType && !auditSchemaService.EntityTypeExists(entityType))
        {
            return NotFound();
        }

        var result = await auditHistoryService.GetHistoryAsync(queryParams, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Returns the distinct entity types that have at least one audit record, sorted alphabetically.
    /// </summary>
    [HttpGet("schema/entity-types")]
    [ProducesResponseType(typeof(AuditEntityTypesResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuditEntityTypesResponse>> GetEntityTypes(CancellationToken cancellationToken)
    {
        var entityTypes = await auditHistoryService.GetRecordedEntityTypesAsync(cancellationToken);
        return Ok(new AuditEntityTypesResponse { EntityTypes = entityTypes });
    }

    /// <summary>
    /// Returns the auditable fields for the given entity type.
    /// </summary>
    [HttpGet("schema/entity-types/{entityType}/fields")]
    [ProducesResponseType(typeof(AuditEntityFieldsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<AuditEntityFieldsResponse> GetEntityTypeFields(string entityType)
    {
        var fields = auditSchemaService.GetFields(entityType);
        return fields is null ? NotFound() : Ok(fields);
    }
}
