using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Unified.DataMigration.Services;
using Unified.Db;

// Regression receipts are captured INSIDE real rollback-only DML, before the graph disappears.
// No private names/notes/photos or source audit payloads are exported.
internal static class AdverseGraphProof
{
    internal static async Task CaptureAsync(
        UnifiedDbContext db,
        IConfiguration config,
        SsWriteSession session,
        CancellationToken ct
    )
    {
        if (
            db.Database.GetDbConnection().Database
            is not (
                "unified_ss_rehearsal_adverse_valid_v11"
                or "unified_ss_rehearsal_adverse_valid_v12"
                or "unified_ss_rehearsal_adverse_valid_v13"
                or "unified_ss_rehearsal_adverse_valid_v16"
                or "unified_ss_rehearsal_adverse_valid_v18"
            )
        )
            return;
        var connectionString = config["DataMigration:Sources:SS:ConnectionString"];
        var sourceDatabase = new Npgsql.NpgsqlConnectionStringBuilder(connectionString).Database;
        if (
            sourceDatabase is not ("ss_adverse_valid_v11" or "ss_adverse_valid_v18")
            || (
                sourceDatabase == "ss_adverse_valid_v18"
                && db.Database.GetDbConnection().Database != "unified_ss_rehearsal_adverse_valid_v18"
            )
        )
            throw new InvalidOperationException("Adverse graph requires the exact source clone.");
        await using var source = new Npgsql.NpgsqlConnection(connectionString);
        await source.OpenAsync(ct);
        await using var transaction = await source.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        await using (var readOnly = new Npgsql.NpgsqlCommand("SET TRANSACTION READ ONLY", source, transaction))
            await readOnly.ExecuteNonQueryAsync(ct);
        var cases = new List<object>();
        await using (
            var command = new Npgsql.NpgsqlCommand(
                """SELECT case_id,source_table,source_id,expected_outcome,class_code FROM synthetic.case_manifest ORDER BY case_id,source_table,source_id""",
                source,
                transaction
            )
        )
        await using (var reader = await command.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct))
                cases.Add(
                    new
                    {
                        Case = reader.GetString(0),
                        Table = reader.GetString(1),
                        Id = reader.GetString(2),
                        Expected = reader.GetString(3),
                        Class = reader.GetString(4),
                    }
                );
        await transaction.RollbackAsync(ct);
        var links = await db
            .DataMigrationRecords.AsNoTracking()
            .Where(x => x.Source == "SS")
            .Select(x => new
            {
                x.EntityType,
                x.LegacyId,
                x.TargetKey,
                x.TargetType,
            })
            .ToListAsync(ct);
        var shifts = await db
            .ShiftEntries.AsNoTracking()
            .Select(x => new
            {
                x.Id,
                x.EventId,
                Status = x.Event!.StatusTypeCode,
                Start = x.Event.StartAtUtc,
                End = x.Event.EndAtUtc,
            })
            .ToListAsync(ct);
        var duties = await db
            .AssignmentEntries.AsNoTracking()
            .Select(x => new
            {
                x.Id,
                x.EventId,
                x.AssignmentSeriesId,
                EventSeriesId = x.Event!.EventSeriesId,
            })
            .ToListAsync(ct);
        var shiftIds = shifts.Select(x => x.Id.ToString()).ToHashSet();
        var dutyIds = duties.Select(x => x.Id.ToString()).ToHashSet();
        var dangling =
            links.Count(x => x.EntityType == "Shift" && !shiftIds.Contains(x.TargetKey))
            + links.Count(x => x.EntityType is "Duty" or "DutySlot" && !dutyIds.Contains(x.TargetKey));
        var created = await db
            .DataMigrationRecords.AsNoTracking()
            .Where(x => x.Source == "SS" && x.CreatedByRunId == session.Run.Id)
            .Select(x => new { x.TargetType, x.TargetKey })
            .Distinct()
            .CountAsync(ct);
        session.Outcome(
            "AdverseCounterValidation",
            session.Run.Id.ToString(),
            created == session.Inserted ? "Migrated" : "Failed",
            created == session.Inserted ? "DistinctPrivateCreatedTargetsMatchCounter" : "PrivateTargetCounterMismatch"
        );
        session.Outcome(
            "AdverseGraphValidation",
            session.Run.Id.ToString(),
            dangling == 0 ? "Migrated" : "Failed",
            dangling == 0 ? "NoDanglingPrivateSchedulingCrosswalks" : "DanglingPrivateSchedulingCrosswalk"
        );
        var path = Path.GetFullPath($"docker/migration/artifacts/adverse-v11-graph-{session.Run.Id}.json");
        if (File.Exists(path))
            throw new InvalidOperationException("Adverse graph proof is immutable.");
        await File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(
                new
                {
                    RunId = session.Run.Id,
                    SourceDatabase = source.Database,
                    TargetDatabase = db.Database.GetDbConnection().Database,
                    Cases = cases,
                    Links = links,
                    Shifts = shifts,
                    Duties = duties,
                    Outcomes = session.Outcomes.Select(x => new
                    {
                        x.EntityType,
                        x.LegacyId,
                        x.Outcome,
                        x.Reason,
                    }),
                    DanglingSchedulingCrosswalks = dangling,
                    DistinctPrivateCreatedTargets = created,
                    InsertedCounter = session.Inserted,
                    Applied = false,
                    RollbackOnly = true,
                }
            ),
            ct
        );
        Console.WriteLine(
            JsonSerializer.Serialize(
                new
                {
                    Proof = "ActualPrivateSchedulingGraph",
                    Cases = cases.Count,
                    ShiftRoots = shifts.Count,
                    DutyRoots = duties.Count,
                    DanglingSchedulingCrosswalks = dangling,
                }
            )
        );
    }
}
