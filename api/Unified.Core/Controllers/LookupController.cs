using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Unified.Core.Models;
using Unified.Core.Services;

namespace Unified.Core.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class LookupController(ILookupService lookupService) : ControllerBase
{
    [HttpGet("{codeType}")]
    [ProducesResponseType(typeof(IEnumerable<LookupCodeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    /// <summary>
    /// Gets all lookup values for the given code type.
    /// </summary>
    /// <param name="codeType">Lookup code type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Lookup values when a code type exists; otherwise 404.</returns>
    public async Task<ActionResult<IEnumerable<LookupCodeResponse>>> GetAll(
        [FromRoute] LookupCodeTypes codeType,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var lookupValues = await lookupService.GetAllAsync(codeType, cancellationToken);
            return Ok(lookupValues);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
