using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Unified.DataMigration.FeatureFlags;
using Unified.DataMigration.Options;
using Unified.DataMigration.Sources;
using Unified.Db;
using Unified.Db.Models.DataMigration;

namespace Unified.DataMigration.Services;

public sealed class SsMigrationExecutor(
    UnifiedDbContext db,
    IConfiguration configuration,
    IOptions<DataMigrationOptions> options,
    IOptions<DataMigrationFeatureFlags> flags,
    DataMigrationControlService controls,
    SsSnapshotReader reader,
    SsMyTeamWriter myTeam,
    SsTrainingWriter training,
    SsSchedulingWriter scheduling
)
{
    public async Task ExecuteAsync(string job, CancellationToken ct)
    {
        var source = options.Value.Sources.SS;
        var settings = source.Jobs.For(job);
        if (!flags.Value.Enabled || !source.Enabled || !settings.Enabled)
            throw new InvalidOperationException("SS migration job is disabled.");
        // Inspect raw configuration: a code default of false is not explicit operator consent.
        foreach (var key in new[] { "ExpireRegions", "ExpireLocations", "ExpireCourtRooms" })
            if (!bool.TryParse(configuration[$"JCInterface:{key}"], out var value) || value)
                throw new InvalidOperationException("SS migration requires explicit false JC expiry flags.");
        if (source.CutoverDate != new DateOnly(2026, 12, 1))
            throw new InvalidOperationException("SS CutoverDate must explicitly be 2026-12-01.");
        var run =
            await controls.TryStartJobAsync(job, settings.DryRun, source.CutoverDate.Value, ct)
            ?? throw new InvalidOperationException("SS job is paused, already running, or its lineage is not ready.");
        var session = new SsWriteSession(db, run);
        try
        {
            if (job == "Report")
            {
                // There is deliberately no success path until the actual business-query oracles are integrated.
                session.Outcome(
                    "Reconciliation",
                    run.Id.ToString(),
                    "NeedsAction",
                    "ActualQueryComparisonNotIntegrated"
                );
                await PersistAsync(run, session, ct);
                return;
            }
            if (settings.DryRun && job != "MyTeam")
            {
                // A dry-run parent rolled back its real targets; never borrow live crosswalks as fake candidates.
                session.Outcome("DryRunLineage", run.Id.ToString(), "Rejected", "MissingIsolatedParentCandidates");
                await PersistAsync(run, session, ct);
                return;
            }
            if (settings.DryRun && await db.DataMigrationRecords.AnyAsync(x => x.Source == "SS", ct))
            {
                session.Outcome(
                    "DryRunLineage",
                    run.Id.ToString(),
                    "Rejected",
                    "DryRunRequiresIsolatedTargetCandidates"
                );
                await PersistAsync(run, session, ct);
                return;
            }
            var tables = job switch
            {
                "MyTeam" => SsMyTeamWriter.Tables,
                "Training" => SsTrainingWriter.Tables,
                "Scheduling" => SsSchedulingWriter.Tables,
                _ => throw new ArgumentOutOfRangeException(nameof(job)),
            };
            var rows = await reader.ReadAsync(tables, settings.BatchSize, ct);
            var boundary = new DateTimeOffset(
                TimeZoneInfo.ConvertTimeToUtc(
                    source.HistoryStartDate.ToDateTime(TimeOnly.MinValue),
                    TimeZoneInfo.FindSystemTimeZoneById("America/Vancouver")
                )
            );
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            // A durable row lock fences recovery/rollback while DML is in flight.
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"DataMigrationRuns\" WHERE \"Id\" = {run.Id} AND \"Status\" = 'Running' FOR UPDATE",
                ct
            );
            var fenced = await db.DataMigrationRuns.AsNoTracking().SingleAsync(x => x.Id == run.Id, ct);
            if (fenced.Status != "Running")
                throw new InvalidOperationException("SS run fence was revoked.");
            var keys = new[] { "SS", $"SS:{job}" };
            if (
                await db.DataMigrationControls.AnyAsync(
                    x => keys.Contains(x.Source) && (x.IsPaused || x.AbortRequestedOn != null),
                    ct
                )
            )
                throw new InvalidOperationException("SS job was stopped before business writes.");
            if (job == "MyTeam")
                await myTeam.WriteAsync(rows, session, boundary, ct);
            if (job == "Training")
                await training.WriteAsync(rows, session, boundary, ct);
            if (job == "Scheduling")
                await scheduling.WriteAsync(rows, session, boundary, ct);
            if (settings.DryRun)
            {
                // Exactly the same EF DML and PostgreSQL constraints, not a bypassed writer.
                await db.SaveChangesAsync(ct);
                await tx.RollbackAsync(ct);
                db.ChangeTracker.Clear();
            }
            else
            {
                await PersistAsync(run, session, ct);
                await tx.CommitAsync(ct);
                return;
            }
            await PersistAsync(run, session, ct);
        }
        catch (Exception ex)
        {
            db.ChangeTracker.Clear();
            var persisted = await db.DataMigrationRuns.SingleAsync(x => x.Id == run.Id, CancellationToken.None);
            persisted.Status = "Failed";
            persisted.CompletedOn = DateTimeOffset.UtcNow;
            persisted.FailedCount++;
            db.DataMigrationFailures.Add(
                new DataMigrationFailure
                {
                    RunId = run.Id,
                    Source = "SS",
                    ErrorType = ex is OperationCanceledException ? "Canceled" : "ExecutionFailed",
                    ErrorMessage =
                        "SS job failed; business transaction rolled back. Inspect safe source outcomes and configuration.",
                    OccurredOn = DateTimeOffset.UtcNow,
                }
            );
            await db.SaveChangesAsync(CancellationToken.None);
            throw new InvalidOperationException("SS migration failed; inspect migration-only run evidence.");
        }
    }

    private async Task PersistAsync(DataMigrationRun run, SsWriteSession session, CancellationToken ct)
    {
        var persisted = await db.DataMigrationRuns.SingleAsync(x => x.Id == run.Id, ct);
        db.DataMigrationOutcomes.AddRange(session.Outcomes);
        persisted.ScannedCount = session.Outcomes.Count;
        persisted.InsertedCount = session.Inserted;
        persisted.UnchangedCount = session.Outcomes.Count(x =>
            x.Reason is "ExistingCrosswalk" or "ExistingGroupCrosswalk"
        );
        persisted.SkippedCount = session.Outcomes.Count(x => x.Outcome == "Excluded");
        persisted.RejectedCount = session.Outcomes.Count(x => x.Outcome == "Rejected");
        persisted.FailedCount = session.Outcomes.Count(x => x.Outcome == "Failed");
        persisted.Status =
            persisted.FailedCount + persisted.RejectedCount > 0 ? "Failed"
            : session.Outcomes.Any(x => x.Outcome is "NeedsAction" or "DroppedBadData") ? "NeedsAction"
            : "Completed";
        persisted.CompletedOn = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}
