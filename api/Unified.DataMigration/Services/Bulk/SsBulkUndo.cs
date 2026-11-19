using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using Unified.Audit.Options;
using Unified.Db;
using Unified.Db.Models.DataMigration;
using Unified.Db.Models.UserManagement;

namespace Unified.DataMigration.Services.Bulk;

/// <summary>
/// Run-scoped undo for a Bulk Training or Scheduling run (Bulk captures no rollback journal, so
/// <see cref="SsRollbackService"/> refuses it with <c>MissingOriginalRollbackJournal</c>).
/// The crosswalk is the undo ledger: business rows the run inserted (receipt <c>Insert</c>), carbine
/// profiles it composed (receipt <c>Compose</c>) and crosswalk rows it created/linked are removed, then the
/// run is marked <c>RolledBack</c> with a <c>Rollback</c> receipt run. Only insert/link/compose-only runs
/// whose targets are unchanged since the run (xmin equals the receipt version) are eligible; anything else
/// is refused with blockers and nothing is changed. Run outcomes, work items and checkpoints are kept as
/// evidence. <c>Users.UpdatedOn/UpdatedById</c> of composed users cannot be restored (no before-image).
/// Scheduling: see <see cref="SchedulingAsync"/>.
/// </summary>
public sealed class SsBulkUndo(UnifiedDbContext db, IOptions<AuditRecordOptions>? auditOptionsAccessor = null)
{
    private readonly IOptions<AuditRecordOptions> auditOptions =
        auditOptionsAccessor ?? Microsoft.Extensions.Options.Options.Create(new AuditRecordOptions());

    private static readonly string[] AllowedOperations = ["Insert", "Link", "Compose"];

    public sealed record Result(Guid? ReceiptRunId, IReadOnlyList<string> Blockers);

    public async Task<Result> UndoAsync(Guid runId, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var connection = SsBulkStaging.Connection(db);
            await SsBulkStaging.ExecuteAsync(connection, "SELECT pg_advisory_xact_lock(872,1)", ct);
            // Run acquisition also takes (872,1), so once it is held no new run can start. Refuse while a run is
            // active *before* taking table locks: an active Bulk chunk locks these tables in a different order,
            // and waiting on it here could deadlock and abort the migration.
            var running = await SsBulkStaging.QueryAsync(
                connection,
                "SELECT EXISTS (SELECT 1 FROM \"DataMigrationRuns\" WHERE \"Source\"='SS' AND \"Status\"='Running')",
                x => x.GetBoolean(0),
                ct
            );
            if (running.Single())
            {
                await tx.RollbackAsync(ct);
                return new(null, ["SourceJobRunning"]);
            }
            foreach (
                var table in new string[]
                {
                    "DataMigrationRuns",
                    "DataMigrationRecords",
                    "DataMigrationMutations",
                    "Users",
                    "TrainingCategories",
                    "Trainings",
                    "TrainingMandatoryTrainingProfiles",
                    "UserTrainings",
                }.Concat(SchedulingTables.Select(x => x.Table))
            )
                await SsBulkStaging.ExecuteAsync(connection, $"LOCK TABLE \"{table}\" IN SHARE ROW EXCLUSIVE MODE", ct);
            var blockers = new List<string>();
            var runs = await db.DataMigrationRuns.AsNoTracking().Where(x => x.Source == "SS").ToListAsync(ct);
            var run =
                runs.SingleOrDefault(x => x.Id == runId)
                ?? throw new InvalidOperationException("SS bulk undo run was not found.");
            if (runs.Any(x => x.Status == "Running"))
                blockers.Add("SourceJobRunning");
            if (run.Job is not ("Training" or "Scheduling"))
                blockers.Add("BulkUndoSupportsTrainingAndSchedulingOnly");
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
            if (run.Job == "Scheduling")
                return await SchedulingAsync(
                    connection,
                    tx,
                    run,
                    mutations
                        .Select(x => new Receipt(
                            x.Record,
                            x.Operation,
                            x.Version,
                            x.TargetType,
                            x.TargetKey,
                            x.CreatedBy
                        ))
                        .ToList(),
                    blockers,
                    ct
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
            // Categories and carbine requirements carry no receipt. The run inserts them in the same chunk
            // transaction as its courses, so their xmin equals the course receipts' version; any other xmin
            // means the row was added or edited after the run and must not be deleted blindly.
            var courseVersions = trainings.Select(x => x.Version).OfType<string>().Distinct().ToArray();
            var changedRequirements = await SsBulkStaging.QueryAsync(
                connection,
                """
                SELECT count(*) FROM "TrainingMandatoryTrainingProfiles"
                WHERE "TrainingId" = ANY(@trainings) AND NOT (xmin::text = ANY(@versions))
                """,
                x => x.GetInt64(0),
                ct,
                new NpgsqlParameter("trainings", trainingIds),
                new NpgsqlParameter("versions", courseVersions)
            );
            if (changedRequirements.Single() > 0)
                blockers.Add("CreatedCourseRequirementsChanged");
            var categories = await SsBulkStaging.QueryAsync(
                connection,
                """
                SELECT c."Id", c.xmin::text = ANY(@versions) AND c."UpdatedOn" IS NULL AS unchanged,
                  EXISTS (SELECT 1 FROM "Trainings" o WHERE o."TrainingCategoryId"=c."Id"
                    AND NOT (o."Id" = ANY(@trainings))) AS shared
                FROM "TrainingCategories" c
                WHERE c."CreatedOn"=@started
                  AND c."Id" IN (SELECT t."TrainingCategoryId" FROM "Trainings" t WHERE t."Id" = ANY(@trainings))
                """,
                x => (Id: x.GetInt32(0), Unchanged: x.GetBoolean(1), Shared: x.GetBoolean(2)),
                ct,
                new NpgsqlParameter("trainings", trainingIds),
                new NpgsqlParameter("versions", courseVersions),
                new NpgsqlParameter("started", run.StartedOn!.Value)
            );
            if (categories.Any(x => !x.Unchanged))
                blockers.Add("CreatedCategoryChanged");
            if (categories.Any(x => x.Shared))
                blockers.Add("CreatedCategoryHasLaterDependents");
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
            // Only categories this run created (verified unchanged and unshared above) are removed.
            var categoryIds = categories.Select(x => x.Id).ToArray();
            await SsBulkStaging.ExecuteAsync(
                connection,
                "DELETE FROM \"TrainingCategories\" WHERE \"Id\" = ANY(@ids)",
                ct,
                new NpgsqlParameter("ids", categoryIds)
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
            var images = await SsBulkTargets.ImagesAsync<User, Guid>(db, composedUsers, auditOptions.Value, ct);
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
                        + $":Categories={categoryIds.Length}:Composed={composedUsers.Length}:Records={linkedRecords.Length}",
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

    private sealed record Receipt(
        long Record,
        string Operation,
        string? Version,
        string TargetType,
        string TargetKey,
        Guid? CreatedBy
    );

    /// <summary>True when an owned crosswalked target no longer produces its recorded graph fingerprint.</summary>
    private async Task<bool> OwnedFingerprintDriftAsync(
        long[] records,
        HashSet<(string Type, string Key)> ownedRows,
        CancellationToken ct
    )
    {
        var crosswalks = await db
            .DataMigrationRecords.AsNoTracking()
            .Where(x => records.Contains(x.Id))
            .Select(x => new
            {
                x.TargetType,
                x.TargetKey,
                x.AfterHash,
            })
            .ToListAsync(ct);
        try
        {
            foreach (
                var target in crosswalks
                    .Where(x => ownedRows.Contains((x.TargetType, x.TargetKey)))
                    .GroupBy(x => (x.TargetType, x.TargetKey))
            )
            {
                var actual = await SsEfSessionBackend.FingerprintAsync(
                    db,
                    target.Key.TargetType,
                    target.Key.TargetKey,
                    ct
                );
                db.ChangeTracker.Clear();
                if (target.Any(x => x.AfterHash != actual))
                    return true;
            }
            return false;
        }
        catch (SsReject)
        {
            return true;
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    // Scheduling graph tables in delete order (children first).
    private static readonly (string Table, string Type)[] SchedulingTables =
    [
        ("ShiftAssignmentEntryUsers", "ShiftAssignmentEntryUser"),
        ("ShiftAssignmentEntries", "ShiftAssignmentEntry"),
        ("ShiftEntryUsers", "ShiftEntryUser"),
        ("AssignmentEntries", "AssignmentEntry"),
        ("ShiftEntries", "ShiftEntry"),
        ("Events", "Event"),
        ("AssignmentSeries", "AssignmentSeries"),
        ("EventSeries", "EventSeries"),
        ("AssignmentDefinitions", "AssignmentDefinition"),
        ("CourtRooms", "CourtRoom"),
    ];

    private static readonly string[] SchedulingOperations =
    [
        "Insert",
        "Link",
        "Compose",
        "SealNewGraph",
        "Repartition",
    ];

    private static readonly string[] SchedulingInsertedTypes =
    [
        "CourtRoom",
        "AssignmentDefinition",
        "AssignmentSeries",
        "ShiftEntry",
        "AssignmentEntry",
    ];

    /// <summary>
    /// Scheduling undo. Every crosswalk row the run touched must have been created by it (no receipt from any
    /// other run), so the run's receipts are the complete ledger. Owned rows are the targets it inserted plus
    /// their structural children (shift/entry events, series event series and occurrences, shift users,
    /// shift-entry links and link users). Bulk writes each chunk in one transaction and stamps receipts with
    /// that transaction's id, so every owned row must still carry one of the run's chunk versions (xmin), and
    /// no row outside the owned set may carry one (the run modified something it does not own). Later
    /// dependents (rows referencing owned rows that are not owned, including set-null references that would
    /// silently detach them) refuse the undo. Deletes are raw SQL (no audit rows), like Training undo.
    /// </summary>
    private async Task<Result> SchedulingAsync(
        NpgsqlConnection connection,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx,
        DataMigrationRun run,
        List<Receipt> mutations,
        List<string> blockers,
        CancellationToken ct
    )
    {
        var runId = run.Id;
        if (mutations.Any(x => !SchedulingOperations.Contains(x.Operation)))
            blockers.Add("BulkUndoRequiresInsertLinkComposeOnlyRun");
        var inserted = mutations.Where(x => x.Operation == "Insert").ToList();
        if (inserted.Any(x => x.CreatedBy != runId))
            blockers.Add("InsertReceiptNotOwnedByRun");
        // A record rebound within the run to a target the run created is owned like an insert (receipts carry
        // the record's current target, so a superseded target that still exists is caught by the xmin scan).
        inserted.AddRange(mutations.Where(x => x.Operation == "Repartition" && x.CreatedBy == runId));
        if (inserted.Any(x => !SchedulingInsertedTypes.Contains(x.TargetType)))
            blockers.Add("UnsupportedInsertedTargetType");
        var records = mutations.Select(x => x.Record).Distinct().ToArray();
        var foreignReceipts = await SsBulkStaging.QueryAsync(
            connection,
            """
            SELECT count(*) FROM "DataMigrationMutations"
            WHERE "DataMigrationRecordId" = ANY(@records) AND "RunId" <> @run
            """,
            x => x.GetInt64(0),
            ct,
            new NpgsqlParameter("records", records),
            new NpgsqlParameter("run", runId)
        );
        if (foreignReceipts.Single() > 0)
            blockers.Add("RunTouchedPreExistingCrosswalk");
        var stamped = await SsBulkStaging.QueryAsync(
            connection,
            """
            SELECT count(*) FROM "DataMigrationRecords"
            WHERE "LastAppliedRunId"=@run AND NOT ("Id" = ANY(@records))
            """,
            x => x.GetInt64(0),
            ct,
            new NpgsqlParameter("run", runId),
            new NpgsqlParameter("records", records)
        );
        if (stamped.Single() > 0)
            blockers.Add("RunUpdatedPreExistingCrosswalk");
        long[] Keys(string type) =>
            inserted
                .Where(x => x.TargetType == type)
                .Select(x => long.Parse(x.TargetKey, System.Globalization.CultureInfo.InvariantCulture))
                .Distinct()
                .ToArray();
        var owned = SchedulingInsertedTypes.ToDictionary(x => x, Keys, StringComparer.Ordinal);
        // Graph-shaping receipts may only target rows the run itself inserted.
        var insertedKeys = inserted.Select(x => (x.TargetType, x.TargetKey)).ToHashSet();
        if (
            mutations.Any(x =>
                x.Operation is "Compose" or "SealNewGraph" or "Repartition"
                && !insertedKeys.Contains((x.TargetType, x.TargetKey))
            )
        )
            blockers.Add("RunModifiedPreExistingTarget");

        async Task<long[]> Ids(string sql, params NpgsqlParameter[] parameters) =>
            (
                await SsBulkStaging.QueryAsync(
                    connection,
                    sql,
                    x => Convert.ToInt64(x.GetValue(0), System.Globalization.CultureInfo.InvariantCulture),
                    ct,
                    parameters
                )
            )
                .Distinct()
                .ToArray();
        var found = new Dictionary<string, long[]>(StringComparer.Ordinal);
        foreach (var (table, type) in SchedulingTables.Where(x => owned.ContainsKey(x.Type)))
            found[type] = await Ids(
                $"SELECT \"Id\" FROM \"{table}\" WHERE \"Id\" = ANY(@ids)",
                new NpgsqlParameter("ids", owned[type])
            );
        if (owned.Any(x => found[x.Key].Length != x.Value.Length))
            blockers.Add("InsertedTargetMissing");
        var shifts = owned["ShiftEntry"];
        var entries = owned["AssignmentEntry"];
        var eventSeries = await Ids(
            "SELECT \"EventSeriesId\" FROM \"AssignmentSeries\" WHERE \"Id\" = ANY(@ids)",
            new NpgsqlParameter("ids", owned["AssignmentSeries"])
        );
        var events = await Ids(
            """
            SELECT "EventId" FROM "ShiftEntries" WHERE "Id" = ANY(@shifts)
            UNION SELECT "EventId" FROM "AssignmentEntries" WHERE "Id" = ANY(@entries)
            UNION SELECT "Id" FROM "Events" WHERE "EventSeriesId" = ANY(@series)
            """,
            new NpgsqlParameter("shifts", shifts),
            new NpgsqlParameter("entries", entries),
            new NpgsqlParameter("series", eventSeries)
        );
        var shiftUsers = await Ids(
            "SELECT \"Id\" FROM \"ShiftEntryUsers\" WHERE \"ShiftEntryId\" = ANY(@shifts)",
            new NpgsqlParameter("shifts", shifts)
        );
        var links = await Ids(
            """
            SELECT "Id" FROM "ShiftAssignmentEntries"
            WHERE "ShiftEntryId" = ANY(@shifts) OR "AssignmentEntryId" = ANY(@entries)
            """,
            new NpgsqlParameter("shifts", shifts),
            new NpgsqlParameter("entries", entries)
        );
        var linkUsers = await Ids(
            "SELECT \"Id\" FROM \"ShiftAssignmentEntryUsers\" WHERE \"ShiftAssignmentEntryId\" = ANY(@links)",
            new NpgsqlParameter("links", links)
        );
        var delete = new Dictionary<string, long[]>(StringComparer.Ordinal)
        {
            ["ShiftAssignmentEntryUser"] = linkUsers,
            ["ShiftAssignmentEntry"] = links,
            ["ShiftEntryUser"] = shiftUsers,
            ["AssignmentEntry"] = entries,
            ["ShiftEntry"] = shifts,
            ["Event"] = events,
            ["AssignmentSeries"] = owned["AssignmentSeries"],
            ["EventSeries"] = eventSeries,
            ["AssignmentDefinition"] = owned["AssignmentDefinition"],
            ["CourtRoom"] = owned["CourtRoom"],
        };
        // Versions of receipts on rows the run owns are its chunk transactions. A Link to a pre-existing target
        // (a Scheduling row or a lookup such as a SubCategory) records that target's own earlier version, which
        // is not a run write and must not be scanned for.
        var ownedRows = SchedulingTables
            .SelectMany(t =>
                delete[t.Type].Select(id => (t.Type, id.ToString(System.Globalization.CultureInfo.InvariantCulture)))
            )
            .ToHashSet();
        var versions = mutations
            .Where(x => x.Operation != "Link" || ownedRows.Contains((x.TargetType, x.TargetKey)))
            .Select(x => x.Version)
            .OfType<string>()
            .SelectMany(x => x.Split(':', StringSplitOptions.RemoveEmptyEntries))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        long drifted = 0,
            foreign = 0;
        foreach (var (table, type) in SchedulingTables)
        {
            // Owned rows still at one of the run's chunk versions; nothing else carries one.
            var counts = await SsBulkStaging.QueryAsync(
                connection,
                $"""
                SELECT count(*) FILTER (WHERE "Id" = ANY(@ids) AND NOT (xmin::text = ANY(@versions))),
                       count(*) FILTER (WHERE NOT ("Id" = ANY(@ids)) AND xmin::text = ANY(@versions))
                FROM "{table}"
                """,
                x => (x.GetInt64(0), x.GetInt64(1)),
                ct,
                new NpgsqlParameter("ids", delete[type]),
                new NpgsqlParameter("versions", versions)
            );
            drifted += counts.Single().Item1;
            foreign += counts.Single().Item2;
        }
        if (drifted > 0)
            blockers.Add("OwnedTargetDrift");
        if (foreign > 0)
            blockers.Add("RunModifiedPreExistingTarget");
        // Row versions cannot reveal a later DELETE of an owned child (a shift user, a shift-entry link or its
        // users) because the parents are not rewritten. The run's crosswalks carry the canonical graph
        // fingerprint PerRow uses as its drift oracle (GuardGraphAsync), so every owned crosswalked target must
        // still produce exactly that fingerprint.
        if (!blockers.Contains("InsertedTargetMissing") && await OwnedFingerprintDriftAsync(records, ownedRows, ct))
            blockers.Add("OwnedTargetDrift");
        // Set-null references would silently detach later rows from an owned series.
        var detached = await SsBulkStaging.QueryAsync(
            connection,
            """
            SELECT (SELECT count(*) FROM "AssignmentEntries"
                    WHERE "AssignmentSeriesId" = ANY(@series) AND NOT ("Id" = ANY(@entries)))
                 + (SELECT count(*) FROM "Events" WHERE "EventSeriesId" = ANY(@eventSeries) AND NOT ("Id" = ANY(@events)))
            """,
            x => x.GetInt64(0),
            ct,
            new NpgsqlParameter("series", owned["AssignmentSeries"]),
            new NpgsqlParameter("entries", entries),
            new NpgsqlParameter("eventSeries", eventSeries),
            new NpgsqlParameter("events", events)
        );
        if (detached.Single() > 0)
            blockers.Add("InsertedTargetHasLaterDependents");
        if (blockers.Count > 0)
        {
            await tx.RollbackAsync(ct);
            return new(null, blockers.Distinct().ToList());
        }

        try
        {
            foreach (var (table, type) in SchedulingTables)
                await SsBulkStaging.ExecuteAsync(
                    connection,
                    $"DELETE FROM \"{table}\" WHERE \"Id\" = ANY(@ids)",
                    ct,
                    new NpgsqlParameter("ids", delete[type])
                );
        }
        catch (PostgresException error) when (error.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            // A restrict reference from outside the owned graph (for example a leave entry on an owned event).
            await tx.RollbackAsync(ct);
            return new(null, ["InsertedTargetHasLaterDependents"]);
        }
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
            new NpgsqlParameter("ids", records)
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
        };
        db.DataMigrationRuns.Add(receipt);
        await db.SaveChangesAsync(ct);
        var target = await db.DataMigrationRuns.SingleAsync(x => x.Id == runId, ct);
        target.Status = "RolledBack";
        db.DataMigrationOutcomes.Add(
            new DataMigrationOutcome
            {
                RunId = receipt.Id,
                EntityType = "RollbackProof",
                LegacyId = runId.ToString(),
                Outcome = "Migrated",
                // Compact (varchar 128): R=rooms D=definitions ES/AS=event/assignment series E=events S=shifts
                // AE=assignment entries SU=shift users L=shift-entry links LU=link users Rec=crosswalks.
                Reason =
                    $"BulkCrosswalkUndo:R={delete["CourtRoom"].Length}:D={delete["AssignmentDefinition"].Length}"
                    + $":ES={delete["EventSeries"].Length}:AS={delete["AssignmentSeries"].Length}"
                    + $":E={delete["Event"].Length}:S={delete["ShiftEntry"].Length}"
                    + $":AE={delete["AssignmentEntry"].Length}:SU={delete["ShiftEntryUser"].Length}"
                    + $":L={delete["ShiftAssignmentEntry"].Length}:LU={delete["ShiftAssignmentEntryUser"].Length}"
                    + $":Rec={records.Length}",
            }
        );
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(receipt.Id, []);
    }
}
