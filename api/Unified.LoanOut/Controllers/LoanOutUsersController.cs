using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Unified.Authorization;
using Unified.Authorization.Claims;
using Unified.LoanOut.Models.Calendar;
using Unified.LoanOut.Services;
using Unified.LoanOut.Validators;

namespace Unified.LoanOut.Controllers;

[ApiController]
[Authorize]
[Route("api/loan-out/scheduling/users")]
public sealed class LoanOutUsersController(
    ILoanOutCalendarDataProvider loanOutCalendarDataProvider,
    LoanOutUserSearchRequestValidator loanOutUserSearchRequestValidator
) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(IReadOnlyCollection<LoanOutCalendarUser>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<LoanOutCalendarUser>>> GetData(
        [FromBody] LoanOutUserSearchRequest request,
        CancellationToken cancellationToken
    )
    {
        await loanOutUserSearchRequestValidator.ValidateAndThrowAsync(request, cancellationToken);

        var canViewLeave = User.HasClaim(UnifiedClaimTypes.Permission, Permissions.LoanOutView.ToString());
        if (!canViewLeave)
            return Forbid();
        return Ok(await loanOutCalendarDataProvider.GetUsersAsync(request, cancellationToken));
    }
}
