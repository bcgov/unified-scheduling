using Microsoft.EntityFrameworkCore;
using Npgsql;
using Unified.Db;
using Unified.Db.Models.DataMigration;

namespace Unified.DataMigration.Services;

public sealed class DataMigrationControlService(UnifiedDbContext dbContext)
{
    public async Task<bool> RecoverStaleRunAsync(Guid runId, DateTimeOffset staleBefore, CancellationToken ct)
    {
        if (staleBefore >= DateTimeOffset.UtcNow)
            throw new InvalidOperationException("Recovery requires an explicit past stale boundary.");
        await using var tx = await dbContext.Database.BeginTransactionAsync(ct);
        await dbContext.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(872, 1)", ct);
        // NOWAIT refuses recovery while the worker holds its business transaction fence.
        // A worker still reading SS must reacquire/check this row before writing any business data.
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"DataMigrationRuns\" WHERE \"Id\" = {runId} FOR UPDATE NOWAIT",
            ct
        );
        var run = await dbContext.DataMigrationRuns.SingleOrDefaultAsync(x => x.Id == runId && x.Source == "SS", ct);
        if (run?.Status != "Running" || run.StartedOn is null || run.StartedOn >= staleBefore)
            return false;
        run.Status = "Abandoned";
        run.CompletedOn = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    public async Task<DataMigrationRun?> TryStartJobAsync(
        string job,
        bool dryRun,
        DateOnly cutover,
        CancellationToken cancellationToken
    )
    {
        if (job is not ("MyTeam" or "Training" or "Scheduling" or "Report"))
            throw new ArgumentOutOfRangeException(nameof(job));
        if (cutover != new DateOnly(2026, 12, 1))
            throw new InvalidOperationException("Invalid SS cutover.");
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
        var parent = runs.Where(x => x.Job == "MyTeam" && x.DryRun == dryRun)
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
                    var latest = runs.Where(x => x.Job == dependency && x.DryRun == dryRun)
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
                        ConstraintName: "IX_DataMigrationRuns_Source_Status",
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
