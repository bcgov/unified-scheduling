using Microsoft.EntityFrameworkCore;
using Npgsql;
using Unified.Db;
using Unified.Db.Models.DataMigration;
using Unified.Db.Models.UserManagement;

namespace Unified.DataMigration.Services.Bulk;

/// <summary>
/// Run-scoped undo for a Bulk Training run (Bulk captures no rollback journal, so
/// <see cref="SsRollbackService"/> refuses it with <c>MissingOriginalRollbackJournal</c>).
/// The crosswalk is the undo ledger: business rows the run inserted (receipt <c>Insert</c>), carbine
/// profiles it composed (receipt <c>Compose</c>) and crosswalk rows it created/linked are removed, then the
/// run is marked <c>RolledBack</c> with a <c>Rollback</c> receipt run. Only insert/link/compose-only runs
/// whose targets are unchanged since the run (xmin equals the receipt version) are eligible; anything else
/// is refused with blockers and nothing is changed. Run outcomes, work items and checkpoints are kept as
/// evidence. <c>Users.UpdatedOn/UpdatedById</c> of composed users cannot be restored (no before-image).
/// </summary>
public sealed class SsBulkUndo(UnifiedDbContext db)
{
    private static readonly string[] AllowedOperations = ["Insert", "Link", "Compose"];

    public sealed record Result(Guid? ReceiptRunId, IReadOnlyList<string> Blockers);

    public async Task<Result> UndoAsync(Guid runId, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var connection = (NpgsqlConnection)db.Database.GetDbConnection();
            await SsBulkStaging.ExecuteAsync(connection, "SELECT pg_advisory_xact_lock(872,1)", ct);
            foreach (
                var table in new[]
                {
                    "DataMigrationRuns",
                    "DataMigrationRecords",
                    "DataMigrationMutations",
                    "Users",
                    "TrainingCategories",
                    "Trainings",
                    "TrainingMandatoryTrainingProfiles",
                    "UserTrainings",
                }
            )
                await SsBulkStaging.ExecuteAsync(connection, $"LOCK TABLE \"{table}\" IN SHARE ROW EXCLUSIVE MODE", ct);
            var blockers = new List<string>();
            var runs = await db.DataMigrationRuns.AsNoTracking().Where(x => x.Source == "SS").ToListAsync(ct);
            var run =
                runs.SingleOrDefault(x => x.Id == runId)
                ?? throw new InvalidOperationException("SS bulk undo run was not found.");
            if (runs.Any(x => x.Status == "Running"))
                blockers.Add("SourceJobRunning");
            if (run.Job != "Training")
                blockers.Add("BulkUndoSupportsTrainingOnly");
            if (run.DryRun || run.Status is "RolledBack" or "Running")
                blockers.Add("RunNotApplied");
            if (run.RollbackJournalKeyId is not null)
                blockers.Add("JournaledRunUseRollbackService");
            // A PerRow run without a journal (journaling disabled) must never be undone here: only runs
            // whose audited trigger evidence bound PersistenceMode=Bulk are eligible.
            var evidence = await db
                .AuditRecords.AsNoTracking()
                .Where(x =>
                    x.EntityType == "DataMigrationOperation"
                    && x.TableName == "DataMigrationRuns"
                    && x.Action == "Added"
                    && x.EntityPK == run.Id.ToString("D")
                )
                .Select(x => x.NewValues)
                .ToListAsync(ct);
            if (
                !evidence.Any(x => SsMigrationExecutor.IsTriggerEvidence(x, run, Options.MigrationPersistenceMode.Bulk))
            )
                blockers.Add("RunNotBulkMode");
            var live = runs.Where(x => x.Id != run.Id && !x.DryRun && x.Status != "RolledBack" && x.Job != "Rollback");
            if (live.Any(x => x.Job == run.Job && x.StartedOn > run.StartedOn))
                blockers.Add("LaterSameJobRunMustRollbackFirst");
            if (
                live.Any(x =>
                    x.Job is "Scheduling" or "Report"
                    && x.MyTeamRunId == run.MyTeamRunId
                    && x.StartedOn >= run.StartedOn
                )
            )
                blockers.Add("DependentJobsMustRollbackFirst");

            var mutations = await SsBulkStaging.QueryAsync(
                connection,
                """
                SELECT m."DataMigrationRecordId", m."Operation", m."Version", r."EntityType", r."TargetType",
                       r."TargetKey", r."CreatedByRunId"
                FROM "DataMigrationMutations" m JOIN "DataMigrationRecords" r ON r."Id"=m."DataMigrationRecordId"
                WHERE m."RunId"=@run ORDER BY m."Sequence"
                """,
                x =>
                    (
                        Record: x.GetInt64(0),
                        Operation: x.GetString(1),
                        Version: x.IsDBNull(2) ? null : x.GetString(2),
                        EntityType: x.GetString(3),
                        TargetType: x.GetString(4),
                        TargetKey: x.GetString(5),
                        CreatedBy: x.IsDBNull(6) ? (Guid?)null : x.GetGuid(6)
                    ),
                ct,
                new NpgsqlParameter("run", runId)
            );
            if (mutations.Any(x => !AllowedOperations.Contains(x.Operation)))
                blockers.Add("BulkUndoRequiresInsertLinkComposeOnlyRun");
            var inserted = mutations.Where(x => x.Operation == "Insert").ToList();
            if (inserted.Any(x => x.CreatedBy != runId))
                blockers.Add("InsertReceiptNotOwnedByRun");
            var linkedRecords = mutations
                .Where(x => x.Operation is "Insert" or "Link")
                .Select(x => x.Record)
                .Distinct()
                .ToArray();
            var stamped = await SsBulkStaging.QueryAsync(
                connection,
                """
                SELECT count(*) FROM "DataMigrationRecords"
                WHERE "LastAppliedRunId"=@run AND NOT ("Id" = ANY(@linked))
                """,
                x => x.GetInt64(0),
                ct,
                new NpgsqlParameter("run", runId),
                new NpgsqlParameter("linked", linkedRecords)
            );
            if (stamped.Single() > 0)
                blockers.Add("RunUpdatedPreExistingCrosswalk");

            var trainings = inserted.Where(x => x.TargetType == "Training").ToList();
            var histories = inserted.Where(x => x.TargetType == "UserTraining").ToList();
            var composes = mutations.Where(x => x.Operation == "Compose" && x.TargetType == "User").ToList();
            if (inserted.Any(x => x.TargetType is not ("Training" or "UserTraining")))
                blockers.Add("UnsupportedInsertedTargetType");
            var trainingIds = trainings.Select(x => int.Parse(x.TargetKey)).ToArray();
            var historyIds = histories.Select(x => int.Parse(x.TargetKey)).ToArray();
            var drift = await SsBulkStaging.QueryAsync(
                connection,
                """
                SELECT count(*) FROM (
                  SELECT 'Training' t, "Id", xmin::text v FROM "Trainings" WHERE "Id" = ANY(@trainings)
                  UNION ALL
                  SELECT 'UserTraining', "Id", xmin::text FROM "UserTrainings" WHERE "Id" = ANY(@histories)
                ) c
                """,
                x => x.GetInt64(0),
                ct,
                new NpgsqlParameter("trainings", trainingIds),
                new NpgsqlParameter("histories", historyIds)
            );
            if (drift.Single() != trainingIds.Length + historyIds.Length)
                blockers.Add("InsertedTargetMissing");
            var versions = (
                await SsBulkStaging.QueryAsync(
                    connection,
                    """
                    SELECT 'Training:'||"Id", xmin::text FROM "Trainings" WHERE "Id" = ANY(@trainings)
                    UNION ALL
                    SELECT 'UserTraining:'||"Id", xmin::text FROM "UserTrainings" WHERE "Id" = ANY(@histories)
                    UNION ALL
                    SELECT 'User:'||"Id"::text, xmin::text FROM "Users"
                      WHERE "Id" = ANY(@users) AND "TrainingProfileId" IS NOT NULL
                    """,
                    x => (x.GetString(0), x.GetString(1)),
                    ct,
                    new NpgsqlParameter("trainings", trainingIds),
                    new NpgsqlParameter("histories", historyIds),
                    new NpgsqlParameter("users", composes.Select(x => Guid.Parse(x.TargetKey)).Distinct().ToArray())
                )
            ).ToDictionary(x => x.Item1, x => x.Item2, StringComparer.Ordinal);
            if (
                inserted.Any(x =>
                    versions.GetValueOrDefault($"{x.TargetType}:{x.TargetKey}") is { } v && v != x.Version
                )
                || composes
                    .GroupBy(x => x.TargetKey)
                    .Any(x => versions.GetValueOrDefault($"User:{x.Key}") != x.Last().Version)
            )
                blockers.Add("OwnedTargetDrift");
            var foreign = await SsBulkStaging.QueryAsync(
                connection,
                """
                SELECT count(*) FROM "UserTrainings"
                WHERE "TrainingId" = ANY(@trainings) AND NOT ("Id" = ANY(@histories))
                """,
                x => x.GetInt64(0),
                ct,
                new NpgsqlParameter("trainings", trainingIds),
                new NpgsqlParameter("histories", historyIds)
            );
            if (foreign.Single() > 0)
                blockers.Add("CreatedCourseHasLaterDependents");
            if (blockers.Count > 0)
            {
                await tx.RollbackAsync(ct);
                return new(null, blockers);
            }

            var composedUsers = composes.Select(x => Guid.Parse(x.TargetKey)).Distinct().ToArray();
            await SsBulkStaging.ExecuteAsync(
                connection,
                "UPDATE \"Users\" SET \"TrainingProfileId\"=NULL WHERE \"Id\" = ANY(@users)",
                ct,
                new NpgsqlParameter("users", composedUsers)
            );
            await SsBulkStaging.ExecuteAsync(
                connection,
                "DELETE FROM \"UserTrainings\" WHERE \"Id\" = ANY(@ids)",
                ct,
                new NpgsqlParameter("ids", historyIds)
            );
            await SsBulkStaging.ExecuteAsync(
                connection,
                "DELETE FROM \"TrainingMandatoryTrainingProfiles\" WHERE \"TrainingId\" = ANY(@ids)",
                ct,
                new NpgsqlParameter("ids", trainingIds)
            );
            await SsBulkStaging.ExecuteAsync(
                connection,
                "DELETE FROM \"Trainings\" WHERE \"Id\" = ANY(@ids)",
                ct,
                new NpgsqlParameter("ids", trainingIds)
            );
            // Categories carry no receipt; the run stamps CreatedOn with its StartedOn, so only
            // now-unreferenced categories created at exactly that instant are removed.
            var categories = await SsBulkStaging.ExecuteAsync(
                connection,
                """
                DELETE FROM "TrainingCategories" c WHERE c."CreatedOn"=@started
                AND NOT EXISTS (SELECT 1 FROM "Trainings" t WHERE t."TrainingCategoryId"=c."Id")
                """,
                ct,
                new NpgsqlParameter("started", run.StartedOn!.Value)
            );
            await SsBulkStaging.ExecuteAsync(
                connection,
                "DELETE FROM \"DataMigrationMutations\" WHERE \"RunId\"=@run",
                ct,
                new NpgsqlParameter("run", runId)
            );
            await SsBulkStaging.ExecuteAsync(
                connection,
                "DELETE FROM \"DataMigrationRecords\" WHERE \"Id\" = ANY(@ids)",
                ct,
                new NpgsqlParameter("ids", linkedRecords)
            );

            var receipt = new DataMigrationRun
            {
                Source = "SS",
                Job = "Rollback",
                DryRun = false,
                Status = "Completed",
                StartedOn = DateTimeOffset.UtcNow,
                CompletedOn = DateTimeOffset.UtcNow,
                RollbackOfRunId = runId,
                CutoverDate = run.CutoverDate,
                ScannedCount = mutations.Count,
                UpdatedCount = composedUsers.Length,
            };
            db.DataMigrationRuns.Add(receipt);
            await db.SaveChangesAsync(ct);
            // Clearing a composed profile changes xmin. Refresh the MyTeam User receipts at the restored
            // version, but only when the mapped fingerprint still equals the receipt (no unrelated edit).
            var images = await SsBulkTargets.ImagesAsync<User, Guid>(db, composedUsers, ct);
            var userRecords = await db
                .DataMigrationRecords.AsNoTracking()
                .Where(x => x.Source == "SS" && x.TargetType == "User")
                .Where(x => composes.Select(y => y.TargetKey).Contains(x.TargetKey))
                .OrderBy(x => x.Id)
                .ToListAsync(ct);
            var refresh = new List<SsBulkMutation>();
            foreach (var record in userRecords)
            {
                if (!images.TryGetValue(record.TargetKey, out var image) || image.Fingerprint != record.AfterHash)
                    throw new InvalidOperationException("SS bulk undo composed-user footprint verification failed.");
                refresh.Add(new(record.Id, null, null, "RollbackRefresh", image.Fingerprint, image.Version));
            }
            await SsBulkCrosswalk.AppendMutationsAsync(connection, receipt.Id, refresh, ct);
            var target = await db.DataMigrationRuns.SingleAsync(x => x.Id == runId, ct);
            target.Status = "RolledBack";
            db.DataMigrationOutcomes.Add(
                new DataMigrationOutcome
                {
                    RunId = receipt.Id,
                    EntityType = "RollbackProof",
                    LegacyId = runId.ToString(),
                    Outcome = "Migrated",
                    Reason =
                        $"BulkCrosswalkUndo:Histories={historyIds.Length}:Courses={trainingIds.Length}"
                        + $":Categories={categories}:Composed={composedUsers.Length}:Records={linkedRecords.Length}",
                }
            );
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new(receipt.Id, []);
        }
        finally
        {
            db.ChangeTracker.Clear();
            await db.Database.CloseConnectionAsync();
        }
    }
}
