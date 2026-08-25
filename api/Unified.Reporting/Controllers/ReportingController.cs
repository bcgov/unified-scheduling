using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Unified.Reporting.Models.Reporting;
using Unified.Reporting.Services.Reporting;

namespace Unified.Reporting.Controllers;

[Authorize]
[ApiController]
[Route("api/reports")]
public class ReportingController(IReportQueryService reportQueryService) : ControllerBase
{
    private static readonly HashSet<string> ReservedQueryKeys =
    ["page", "pagesize", "sortby", "sortdir", "tz"];

    [HttpGet("{reportKey}")]
    [ProducesResponseType(typeof(ReportQueryResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReportQueryResult>> Get(
        [FromRoute] string reportKey,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var request = BuildRequestFromQuery();
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

    private ReportQueryRequest BuildRequestFromQuery()
    {
        var page = ParseIntOrDefault("page", 1);
        var pageSize = ParseIntOrDefault("pageSize", 50);
        var sortBy = GetValueOrNull("sortBy");
        var sortDirection = ParseSortDirectionOrDefault("sortDir", SortDirection.Asc);
        var timeZone = GetValueOrNull("tz");

        var filters = Request
            .Query.Where(entry => !ReservedQueryKeys.Contains(entry.Key, StringComparer.OrdinalIgnoreCase))
            .ToDictionary(
                entry => entry.Key,
                entry =>
                    (IReadOnlyCollection<string>)[.. entry
                        .Value.Where(value => !string.IsNullOrWhiteSpace(value))
                        .Select(value => value!)],
                StringComparer.OrdinalIgnoreCase
            );

        return new ReportQueryRequest(filters, page, pageSize, sortBy, sortDirection, timeZone);
    }

    private int ParseIntOrDefault(string key, int fallback)
    {
        var value = GetValueOrNull(key);
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        if (!int.TryParse(value, out var parsed))
        {
            throw new ArgumentException($"Query parameter '{key}' must be an integer.");
        }

        return parsed;
    }

    private SortDirection ParseSortDirectionOrDefault(string key, SortDirection fallback)
    {
        var value = GetValueOrNull(key);
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        if (Enum.TryParse<SortDirection>(value, true, out var parsed))
        {
            return parsed;
        }

        throw new ArgumentException($"Query parameter '{key}' must be either 'asc' or 'desc'.");
    }

    private string? GetValueOrNull(string key)
    {
        if (!Request.Query.TryGetValue(key, out var value))
        {
            return null;
        }

        var resolved = value.FirstOrDefault();
        return string.IsNullOrWhiteSpace(resolved) ? null : resolved;
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