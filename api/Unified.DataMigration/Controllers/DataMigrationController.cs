using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Unified.Authorization.Claims;
using Unified.DataMigration.Models;
using Unified.DataMigration.Services;

namespace Unified.DataMigration.Controllers;

[ApiController]
[Route("api/data-migration/{source}")]
[Authorize(Policy = MigrationPermissions.OperationsPolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[RequireJsonContentType]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status501NotImplemented)]
public sealed class DataMigrationController(DataMigrationOperations operations) : ControllerBase
{
    private Guid Actor =>
        Guid.TryParse(User.FindFirstValue(UnifiedClaimTypes.UserId), out var id) && id != Guid.Empty
            ? id
            : throw new MigrationOperationException(403, "ResolvedMigrationActorRequired");

    private async Task<IActionResult> Invoke(Func<Guid, Task<IActionResult>> action)
    {
        try
        {
            Response.Headers.CacheControl = "no-store";
            Response.Headers.XContentTypeOptions = "nosniff";
            return await action(Actor);
        }
        catch (MigrationOperationException e)
        {
            return Error(e.Status, e.Code);
        }
        catch (PostgresException e) when (e.SqlState is "23505" or "40001" or "40P01" or "55P03")
        {
            return Error(409, "MigrationConcurrentOperation");
        }
        catch (InvalidOperationException)
        {
            return Error(409, "MigrationPreconditionFailed");
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Error(503, "MigrationOperationUnavailable");
        }
    }

    private ObjectResult Error(int status, string code) =>
        new(
            new ProblemDetails
            {
                Status = status,
                Title = "Migration operation refused",
                Extensions = { ["code"] = code },
            }
        )
        {
            StatusCode = status,
        };

    [HttpGet("jobs/{job}/status")]
    [ProducesResponseType(typeof(MigrationStatusDto), StatusCodes.Status200OK)]
    public Task<IActionResult> Status(string source, string job, CancellationToken ct) =>
        Invoke(async _ => Ok(await operations.StatusAsync(source, job, ct)));

    [HttpGet("status")]
    [ProducesResponseType(typeof(IReadOnlyList<MigrationStatusDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> SourceStatus(string source, CancellationToken ct) =>
        Invoke(async _ =>
        {
            DataMigrationOperations.Scope(source);
            var values = new List<MigrationStatusDto>();
            foreach (var job in DataMigrationOperations.Jobs)
                values.Add(await operations.StatusAsync(source, job, ct));
            return Ok(values);
        });

    [HttpGet("runs")]
    [ProducesResponseType(typeof(MigrationPage<MigrationRunDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> SourceRuns(string source, CancellationToken ct, int offset = 0, int limit = 50) =>
        Invoke(async _ => Ok(await operations.RunsAsync(source, null, offset, limit, ct)));

    [HttpGet("runs/{id:guid}")]
    [ProducesResponseType(typeof(MigrationRunDto), StatusCodes.Status200OK)]
    public Task<IActionResult> SourceRun(string source, Guid id, CancellationToken ct) =>
        Invoke(async _ => Ok(await operations.RunAsync(source, null, id, ct)));

    [HttpGet("jobs/{job}/runs")]
    [ProducesResponseType(typeof(MigrationPage<MigrationRunDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> Runs(string source, string job, CancellationToken ct, int offset = 0, int limit = 50) =>
        Invoke(async _ => Ok(await operations.RunsAsync(source, job, offset, limit, ct)));

    [HttpGet("jobs/{job}/runs/{id:guid}")]
    [ProducesResponseType(typeof(MigrationRunDto), StatusCodes.Status200OK)]
    public Task<IActionResult> Run(string source, string job, Guid id, CancellationToken ct) =>
        Invoke(async _ => Ok(await operations.RunAsync(source, job, id, ct)));

    [HttpPost("jobs/{job}/runs")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(MigrationAcceptedDto), StatusCodes.Status202Accepted)]
    public Task<IActionResult> Trigger(
        string source,
        string job,
        [FromBody] MigrationTriggerRequest request,
        CancellationToken ct
    ) =>
        Invoke(async actor =>
        {
            var value = await operations.TriggerAsync(source, job, request.DryRun, actor, ct);
            return Accepted($"/api/data-migration/{source}/jobs/{job}/runs/{value.RunId:D}", value);
        });

    [HttpPost("control/{actionName}")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(MigrationControlDto), StatusCodes.Status200OK)]
    public Task<IActionResult> SourceControl(string source, string actionName, CancellationToken ct) =>
        Invoke(async actor => Ok(await operations.ControlAsync(source, null, actionName, actor, ct)));

    [HttpPost("jobs/{job}/control/{actionName}")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(MigrationControlDto), StatusCodes.Status200OK)]
    public Task<IActionResult> JobControl(string source, string job, string actionName, CancellationToken ct) =>
        Invoke(async actor => Ok(await operations.ControlAsync(source, job, actionName, actor, ct)));

    [HttpPost("jobs/{job}/runs/{id:guid}/recover")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(MigrationRunDto), StatusCodes.Status200OK)]
    public Task<IActionResult> Recover(
        string source,
        string job,
        Guid id,
        MigrationRecoveryRequest request,
        CancellationToken ct
    ) => Invoke(async actor => Ok(await operations.RecoverAsync(source, job, id, request.StaleBeforeUtc, actor, ct)));

    [HttpGet("jobs/{job}/runs/{id:guid}/findings/{kind}")]
    [ProducesResponseType(typeof(MigrationPage<MigrationFindingDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> Findings(
        string source,
        string job,
        Guid id,
        string kind,
        CancellationToken ct,
        int offset = 0,
        int limit = 50
    ) => Invoke(async _ => Ok(await operations.FindingsAsync(source, job, id, kind, offset, limit, ct)));

    [HttpGet("jobs/{job}/runs/{id:guid}/export/{format}")]
    public Task<IActionResult> Export(string source, string job, Guid id, string format, CancellationToken ct) =>
        Invoke(async actor =>
        {
            await operations.RunAsync(source, job, id, ct);
            if (format is not ("csv" or "markdown"))
                throw new MigrationOperationException(400, "InvalidExportFormat");
            return new MigrationExportResult(operations, source, job, id, format, actor);
        });

    [Authorize(Policy = MigrationPermissions.RollbackPolicy)]
    [HttpPost("jobs/{job}/runs/{id:guid}/rollback-preview")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(MigrationRollbackPreviewDto), StatusCodes.Status200OK)]
    public Task<IActionResult> RollbackPreview(string source, string job, Guid id, CancellationToken ct) =>
        Invoke(async actor => Ok(await operations.PreviewAsync(source, job, id, actor, ct)));

    [Authorize(Policy = MigrationPermissions.RollbackPolicy)]
    [HttpGet("jobs/{job}/runs/{id:guid}/rollback-impact")]
    public Task<IActionResult> RollbackImpact(
        string source,
        string job,
        Guid id,
        string digest,
        CancellationToken ct
    ) =>
        Invoke(async actor =>
            File(
                await operations.ImpactAsync(source, job, id, digest, actor, ct),
                "application/json",
                $"ss-{job}-{id:D}-restricted-impact.json"
            )
        );

    [Authorize(Policy = MigrationPermissions.RollbackPolicy)]
    [HttpPost("jobs/{job}/runs/{id:guid}/rollback")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(MigrationRollbackReceiptDto), StatusCodes.Status200OK)]
    public Task<IActionResult> Rollback(
        string source,
        string job,
        Guid id,
        MigrationRollbackRequest request,
        CancellationToken ct
    ) => Invoke(async actor => Ok(await operations.RollbackAsync(source, job, id, request, actor, ct)));

    private sealed class MigrationExportResult(
        DataMigrationOperations operations,
        string source,
        string job,
        Guid id,
        string format,
        Guid actor
    ) : IActionResult
    {
        public async Task ExecuteResultAsync(ActionContext context)
        {
            var response = context.HttpContext.Response;
            response.ContentType = format == "csv" ? "text/csv; charset=utf-8" : "text/markdown; charset=utf-8";
            response.Headers.ContentDisposition =
                $"attachment; filename=\"ss-{job}-{id:D}-findings.{(format == "csv" ? "csv" : "md")}\"";
            response.Headers.CacheControl = "no-store";
            response.Headers.XContentTypeOptions = "nosniff";
            try
            {
                await using var writer = new StreamWriter(response.Body, new UTF8Encoding(false), leaveOpen: true);
                await operations.ExportAsync(
                    source,
                    job,
                    id,
                    format,
                    actor,
                    writer,
                    context.HttpContext.RequestAborted
                );
                await writer.FlushAsync(context.HttpContext.RequestAborted);
            }
            catch
            {
                // Never append raw exception text to a partial CSV/Markdown or call it complete.
                // Missing End/SnapshotComplete identifies an interrupted download.
                context.HttpContext.Abort();
            }
        }
    }
}
