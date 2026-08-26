using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Unified.Common.Reporting;
using Unified.Reporting.Models;
using Unified.Reporting.Services.Reporting;

namespace Unified.Reporting.Controllers;

[Authorize]
[ApiController]
[Route("api/reports")]
public class ReportingController(IReportQueryService reportQueryService) : ControllerBase
{
    [HttpGet("{reportKey}")]
    [ProducesResponseType(typeof(PagedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse>> Get(
        [FromRoute] string reportKey,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var request = ReportQueryRequestParser.FromQuery(Request.Query);
            var result = await reportQueryService.ExecuteAsync(reportKey, request, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BuildBadRequest(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BuildBadRequest(ex.Message);
        }
    }

    private BadRequestObjectResult BuildBadRequest(string detail)
        => BadRequest(
            new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid report query request",
                Detail = detail,
            }
        );
}