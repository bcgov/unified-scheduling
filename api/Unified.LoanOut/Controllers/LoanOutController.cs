using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Unified.LoanOut.Models;
using Unified.LoanOut.Services;
using Unified.LoanOut.Validators;

namespace Unified.LoanOut.Controllers;

[ApiController]
[Authorize]
[Route("api/scheduling/loan-out")]
public sealed class LoanOutController(
    ILoanOutService loanoutService,
    LoanOutRequestValidator loanoutEntryRequestValidator,
    LoanOutSeriesRequestValidator loanoutSeriesRequestValidator
) : ControllerBase
{
    [HttpGet("users/{userId:guid}/entries")]
    [EndpointSummary("Get loan-out entries for a user")]
    [EndpointDescription("Returns entries assigned to the user, ordered by start time descending.")]
    [Authorize(Policy = LoanOutPolicies.LoanOutViewOrUser)]
    [ProducesResponseType(typeof(IReadOnlyList<LoanOutEntryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LoanOutEntryResponse>>> GetLoanOutEntriesByUser(
        Guid userId,
        CancellationToken cancellationToken
    ) => Ok(await loanoutService.GetLoanOutEntriesByUserAsync(userId, cancellationToken));

    [HttpGet("users/{userId:guid}/series")]
    [EndpointSummary("Get loan-out series for a user")]
    [EndpointDescription("Returns recurring loan-out series assigned to the user, ordered by start time descending.")]
    [Authorize(Policy = LoanOutPolicies.LoanOutViewOrUser)]
    [ProducesResponseType(typeof(IReadOnlyList<LoanOutSeriesResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LoanOutSeriesResponse>>> GetLoanOutSeriesByUser(
        Guid userId,
        CancellationToken cancellationToken
    ) => Ok(await loanoutService.GetLoanOutSeriesByUserAsync(userId, cancellationToken));

    [HttpGet("series/{id:int}")]
    [EndpointSummary("Get a loan-out series")]
    [EndpointDescription(
        "Returns the series, assigned users, and persisted occurrence identifiers, or 404 if not found."
    )]
    [Authorize(Policy = LoanOutPolicies.LoanOutViewOrUser)]
    [ProducesResponseType(typeof(LoanOutSeriesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LoanOutSeriesResponse>> GetLoanOutSeriesById(
        int id,
        CancellationToken cancellationToken
    )
    {
        var result = await loanoutService.GetLoanOutSeriesByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("series")]
    [EndpointSummary("Create a recurring loan-out series")]
    [EndpointDescription(
        "Creates the series and materializes its occurrences atomically. Requires unique, non-empty users, a positive loan-out location, an end time, and a recurrence rule bounded by COUNT or UNTIL. Recurrence is limited to 400 occurrences over 365 days."
    )]
    [Authorize(Policy = LoanOutPolicies.LoanOutCreateOrUser)]
    [ProducesResponseType(typeof(LoanOutSeriesResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LoanOutSeriesResponse>> CreateLoanOutSeries(
        [FromBody] LoanOutSeriesRequest request,
        CancellationToken cancellationToken
    )
    {
        await loanoutSeriesRequestValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await loanoutService.CreateLoanOutSeriesAsync(request, cancellationToken);
        return Created($"/api/scheduling/loan-out/series/{result.Id}", result);
    }

    [HttpPut("series/{id:int}")]
    [EndpointSummary("Update a loan-out series")]
    [EndpointDescription(
        "Updates a draft series. Recurrence changes regenerate draft occurrences; otherwise shared fields, loan-out locations, and users propagate to occurrences that have not overridden those values."
    )]
    [Authorize(Policy = LoanOutPolicies.LoanOutEditOrUser)]
    [ProducesResponseType(typeof(LoanOutSeriesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LoanOutSeriesResponse>> UpdateLoanOutSeries(
        int id,
        [FromBody] LoanOutSeriesRequest request,
        CancellationToken cancellationToken
    )
    {
        await loanoutSeriesRequestValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await loanoutService.UpdateLoanOutSeriesAsync(id, request, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("series/{id:int}")]
    [EndpointSummary("Delete a loan-out series")]
    [EndpointDescription(
        "Deletes a draft series, its occurrences, and user associations. Loan-out has no publish or cancel endpoints."
    )]
    [Authorize(Policy = LoanOutPolicies.LoanOutDeleteOrUser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteLoanOutSeries(int id, CancellationToken cancellationToken) =>
        await loanoutService.DeleteLoanOutSeriesAsync(id, cancellationToken) ? NoContent() : NotFound();

    [HttpGet("entries/{id:int}")]
    [EndpointSummary("Get a loan-out entry")]
    [EndpointDescription("Returns a loan-out entry and its assigned users, or 404 if not found.")]
    [Authorize(Policy = LoanOutPolicies.LoanOutViewOrUser)]
    [ProducesResponseType(typeof(LoanOutEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LoanOutEntryResponse>> GetLoanOutEntryById(
        int id,
        CancellationToken cancellationToken
    )
    {
        var result = await loanoutService.GetLoanOutEntryByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("entries")]
    [EndpointSummary("Create a loan-out entry")]
    [EndpointDescription(
        "Creates a calendar event and loan-out entry atomically. Requires unique, non-empty users and a positive loan-out location. When an end time is supplied, it must be later than the start time."
    )]
    [Authorize(Policy = LoanOutPolicies.LoanOutCreateOrUser)]
    [ProducesResponseType(typeof(LoanOutEntryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LoanOutEntryResponse>> CreateLoanOutEntry(
        [FromBody] LoanOutEntryRequest request,
        CancellationToken cancellationToken
    )
    {
        await loanoutEntryRequestValidator.ValidateAndThrowAsync(request, cancellationToken);

        var result = await loanoutService.CreateLoanOutEntryAsync(request, cancellationToken);
        return Created($"/api/scheduling/loan-out/entries/{result.Id}", result);
    }

    [HttpPut("entries/{id:int}")]
    [EndpointSummary("Update a loan-out entry")]
    [EndpointDescription("Updates a draft loan-out entry and synchronizes its assigned users and loan-out location.")]
    [Authorize(Policy = LoanOutPolicies.LoanOutEditOrUser)]
    [ProducesResponseType(typeof(LoanOutEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LoanOutEntryResponse>> UpdateLoanOutEntry(
        int id,
        [FromBody] LoanOutEntryRequest request,
        CancellationToken cancellationToken
    )
    {
        await loanoutEntryRequestValidator.ValidateAndThrowAsync(request, cancellationToken);
        var result = await loanoutService.UpdateLoanOutEntryAsync(id, request, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("entries/{id:int}")]
    [EndpointSummary("Delete a loan-out entry")]
    [EndpointDescription(
        "Deletes a draft loan-out entry, its calendar event, and user associations, or returns 404 if not found."
    )]
    [Authorize(Policy = LoanOutPolicies.LoanOutDeleteOrUser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteLoanOutEntry(int id, CancellationToken cancellationToken) =>
        await loanoutService.DeleteLoanOutEntryAsync(id, cancellationToken) ? NoContent() : NotFound();
}
