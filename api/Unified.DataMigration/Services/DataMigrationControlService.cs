using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using Unified.Audit;
using Unified.DataMigration.Options;
using Unified.Db;
using Unified.Db.Models.DataMigration;

namespace Unified.DataMigration.Services;

public sealed class DataMigrationControlService : IMigrationRunCoordinator
{
    private readonly UnifiedDbContext dbContext;
    private readonly ICurrentActorResolver? actors;
    private readonly IHttpContextAccessor? httpContext;
    private readonly IOptions<DataMigrationOptions>? options;

    // Kept for legacy callers. Without request services, existing trigger evidence is
    // used for attribution; an unidentifiable historical initiator remains unknown.
    public DataMigrationControlService(UnifiedDbContext dbContext) => this.dbContext = dbContext;

    public DataMigrationControlService(
        UnifiedDbContext dbContext,
        ICurrentActorResolver actors,
        IHttpContextAccessor httpContext,
        IOptions<DataMigrationOptions> options
    )
    {
        this.dbContext = dbContext;
        this.actors = actors;
        this.httpContext = httpContext;
        this.options = options;
    }

    public async Task<bool> RecoverStaleRunAsync(Guid runId, DateTimeOffset staleBefore, CancellationToken ct)
    {
        if (staleBefore >= DateTimeOffset.UtcNow)
            throw new InvalidOperationException("Recovery requires an explicit past stale boundary.");
        using var auditScope = new SsMigrationAuditScope(dbContext, requireClean: true);
        await using var tx = await dbContext.Database.BeginTransactionAsync(ct);
        await dbContext.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(872, 1)", ct);
        // NOWAIT refuses recovery while the worker holds its business transaction fence.
        // A worker still reading SS must reacquire/check this row before writing any business data.
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"DataMigrationRuns\" WHERE \"Id\" = {runId} FOR UPDATE NOWAIT",
            ct
        );
        var run = await dbContext.DataMigrationRuns.SingleOrDefaultAsync(x => x.Id == runId && x.Source == "SS", ct);
        if (run?.Status != "Running" || run.StartedOn is null || (run.HeartbeatOn ?? run.StartedOn) >= staleBefore)
            return false;
        run.Status = "Abandoned";
        run.CompletedOn = DateTimeOffset.UtcNow;
        try
        {
            await dbContext.SaveChangesAsync(ct);
            await SsMigrationRunAudit.WriteAsync(dbContext, run.Id, ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            // A rolled-back SaveChanges still leaves accepted values in EF's tracker.
            // Do not let a reused context later persist a phantom terminal state.
            dbContext.ChangeTracker.Clear();
            throw;
        }
        return true;
    }

    public async Task<DataMigrationRun?> TryStartJobAsync(
        string job,
        bool dryRun,
        DateOnly cutover,
        CancellationToken cancellationToken,
        DateOnly? historyStartDate = null
    )
    {
        if (job is not ("MyTeam" or "Training" or "Scheduling" or "Report"))
            throw new ArgumentOutOfRangeException(nameof(job));
        if (cutover != new DateOnly(2026, 12, 1))
            throw new InvalidOperationException("Invalid SS cutover.");
        var actor = actors?.Resolve();
        if (actors is not null && (actor?.ActorUserId is null || actor.ActorUserId == Guid.Empty))
            throw new InvalidOperationException("SS migration acquisition requires a resolved initiator.");
        using var auditScope = new SsMigrationAuditScope(dbContext, requireClean: true);
        await using var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        // All dependency checks and the insertion share this transaction-scoped, source-wide lock.
        await dbContext.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(872, 1)", cancellationToken);
        var controlKeys = new[] { "SS", $"SS:{job}" };
        if (
            await dbContext.DataMigrationControls.AnyAsync(
                x => controlKeys.Contains(x.Source) && (x.IsPaused || x.AbortRequestedOn != null),
                cancellationToken
            )
        )
            return null;
        if (
            await dbContext.DataMigrationRuns.AnyAsync(
                x => x.Source == "SS" && !x.DryRun && x.CutoverDate != null && x.CutoverDate != cutover,
                cancellationToken
            )
        )
            throw new InvalidOperationException("Applied SS cutover is immutable.");
        var runs = await dbContext.DataMigrationRuns.Where(x => x.Source == "SS").ToListAsync(cancellationToken);
        if (runs.Any(x => x.Status == "Running" && (x.Job == job || x.Job == "Legacy")))
            return null;
        var history = historyStartDate ?? new DateOnly(2020, 1, 1);
        // Runs created before HistoryStartDate was persisted used the original 2015 boundary.
        var parent = runs.Where(x =>
                x.Status != "RolledBack"
                && x.Job == "MyTeam"
                && x.DryRun == dryRun
                && (x.HistoryStartDate ?? new DateOnly(2015, 1, 1)) == history
            )
            .OrderByDescending(x => x.StartedOn)
            .FirstOrDefault();
        if (parent?.Status != "Completed")
            parent = null;
        if (job == "MyTeam")
        {
            if (runs.Any(x => x.Status == "Running"))
                return null;
        }
        else
        {
            if (parent is null || runs.Any(x => x.Job is "MyTeam" or "Report" && x.Status == "Running"))
                return null;
            if (job == "Report")
            {
                if (runs.Any(x => x.Status == "Running"))
                    return null;
                foreach (var dependency in new[] { "Training", "Scheduling" })
                {
                    var latest = runs.Where(x =>
                            x.Status != "RolledBack"
                            && x.Job == dependency
                            && x.DryRun == dryRun
                            && (x.HistoryStartDate ?? new DateOnly(2015, 1, 1)) == history
                        )
                        .OrderByDescending(x => x.StartedOn)
                        .FirstOrDefault();
                    if (latest?.Status != "Completed" || latest.MyTeamRunId != parent.Id)
                        return null;
                }
            }
        }
        var run = new DataMigrationRun
        {
            Source = "SS",
            Job = job,
            DryRun = dryRun,
            Status = "Running",
            StartedOn = DateTimeOffset.UtcNow,
            CutoverDate = cutover,
            MyTeamRunId = parent?.Id,
            HistoryStartDate = history,
            HeartbeatOn = DateTimeOffset.UtcNow,
            InitiatedByUserId = actor?.ActorUserId,
            InitiationKind =
                actor is null ? null
                : httpContext?.HttpContext is null ? "Scheduled"
                : "Operator",
            PersistenceMode = options?.Value.Sources.SS.Jobs.For(job).PersistenceMode.ToString(),
        };
        dbContext.DataMigrationRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return run;
    }

    public async Task<DataMigrationRun?> TryStartRunAsync(
        string source,
        bool dryRun,
        CancellationToken cancellationToken = default
    )
    {
        var run = new DataMigrationRun
        {
            Source = source,
            Status = "Running",
            StartedOn = DateTimeOffset.UtcNow,
            DryRun = dryRun,
        };

        dbContext.DataMigrationRuns.Add(run);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return run;
        }
        catch (DbUpdateException ex)
            when (ex.InnerException
                    is PostgresException
                    {
                        SqlState: PostgresErrorCodes.UniqueViolation,
                        // Running-run lease index (replaced IX_DataMigrationRuns_Source_Status in AddSsMigrationJobLineage).
                        ConstraintName: "IX_DataMigrationRuns_Source_Job",
                    }
            )
        {
            dbContext.Entry(run).State = EntityState.Detached;
            return null;
        }
    }

    public async Task CompleteRunAsync(
        DataMigrationRun run,
        string status,
        CancellationToken cancellationToken = default
    )
    {
        var trackedRun = await dbContext
            .DataMigrationRuns.AsTracking()
            .SingleAsync(item => item.Id == run.Id, cancellationToken);
        trackedRun.Status = status;
        trackedRun.CompletedOn = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
