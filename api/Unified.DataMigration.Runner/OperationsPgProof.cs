using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Unified.DataMigration.Models;
using Unified.DataMigration.Services;
using Unified.Db;

// Exact isolated target only. HTTP permissions are exercised by the TestServer suite;
// this harness exercises actual PostgreSQL acquisition/worker/fences/DML/exports.
internal sealed class OperationsPgProof : DbCommandInterceptor
{
    internal IServiceProvider Services { get; set; } = null!;
    internal bool PauseNextSimulation { get; set; }
    internal int Pauses { get; private set; }
    private static readonly Guid Actor = Guid.Parse("a3000000-0000-0000-0000-000000000001");

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default
    )
    {
        if (PauseNextSimulation)
        {
            // Simulation DDL uses raw ADO; inspect the actual session namespace
            // at its first EF read rather than pretending an EF DDL hook ran.
            await using var metadata = command.Connection!.CreateCommand();
            metadata.Transaction = command.Transaction;
            metadata.CommandText = "SELECT current_schema()";
            var schema = (string?)await metadata.ExecuteScalarAsync(cancellationToken);
            if (schema?.StartsWith("ss_sim_", StringComparison.Ordinal) == true)
            {
                PauseNextSimulation = false;
                await using var control = Services.CreateAsyncScope();
                await control
                    .ServiceProvider.GetRequiredService<DataMigrationOperations>()
                    .ControlAsync("SS", "MyTeam", "pause", Actor, cancellationToken);
                Pauses++;
            }
        }
        return result;
    }

    internal async Task RunAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<UnifiedDbContext>();
        if (
            db.Database.GetDbConnection().Database
            is not ("unified_ss_rehearsal_operations_v14" or "unified_ss_rehearsal_operations_v15")
        )
            throw new InvalidOperationException("Operations proof requires its exact isolated target.");
        if (await db.DataMigrationRuns.AnyAsync())
            throw new InvalidOperationException("Operations proof refuses an already exercised fixture.");
        var before = await Footprint(db);
        var auditFloor = await db.AuditRecords.Select(x => (long?)x.Id).MaxAsync() ?? 0;
        var operations = scope.ServiceProvider.GetRequiredService<DataMigrationOperations>();
        var queue = scope.ServiceProvider.GetRequiredService<RecordingDispatcher>();
        var first = await operations.TriggerAsync("SS", "MyTeam", true, Actor, CancellationToken.None);
        Check(queue.Enqueued == first.RunId, "StableAcquiredRunIsDispatched");
        var worker = scope.ServiceProvider.GetRequiredService<SsMigrationExecutor>();
        // Real duplicate-delivery ownership guard, not a fake coordinator.
        await using (var competing = new NpgsqlConnection(db.Database.GetConnectionString()))
        {
            await competing.OpenAsync();
            await using var claim = new NpgsqlCommand("SELECT pg_advisory_lock(873,hashtext(@id))", competing);
            claim.Parameters.AddWithValue("id", first.RunId.ToString("D"));
            await claim.ExecuteNonQueryAsync();
            var refused = false;
            try
            {
                await worker.ExecuteAcquiredAsync(first.RunId, CancellationToken.None);
            }
            catch (InvalidOperationException)
            {
                refused = true;
            }
            Check(
                refused
                    && (await operations.RunAsync("SS", "MyTeam", first.RunId, CancellationToken.None)).Status
                        == "Running",
                "DuplicateOwnerRefusedWithoutRevokingRun"
            );
            await using var release = new NpgsqlCommand("SELECT pg_advisory_unlock_all()", competing);
            await release.ExecuteNonQueryAsync();
        }
        await worker.ExecuteAcquiredAsync(first.RunId, CancellationToken.None);
        Check(
            (await operations.RunAsync("SS", "MyTeam", first.RunId, CancellationToken.None)).Status == "Completed",
            "QueuedActualMyTeamCompleted"
        );
        Check(await db.DataMigrationSimulations.AnyAsync(x => x.RunId == first.RunId), "DurableSimulationManifest");
        var replayRefused = false;
        try
        {
            await worker.ExecuteAcquiredAsync(first.RunId, CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            replayRefused = true;
        }
        Check(replayRefused, "CompletedRunCannotExecuteAgain");
        foreach (var format in new[] { "csv", "markdown" })
        {
            using var writer = new StringWriter();
            await operations.ExportAsync("SS", "MyTeam", first.RunId, format, Actor, writer, CancellationToken.None);
            Check(writer.ToString().Contains("SnapshotComplete", StringComparison.Ordinal), "ActualPgExport:" + format);
        }
        // Trigger a real public control update AFTER private schema cloning. The
        // copied private control still says unpaused; only the explicit public
        // fence can stop this worker. No timing/polling assumptions.
        var second = await operations.TriggerAsync("SS", "MyTeam", true, Actor, CancellationToken.None);
        PauseNextSimulation = true;
        var stopped = false;
        try
        {
            await worker.ExecuteAcquiredAsync(second.RunId, CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            stopped = true;
        }
        Check(stopped && Pauses == 1, "PublicPauseObservedInsidePrivateSimulation");
        Check(
            !await db.DataMigrationSimulations.AnyAsync(x => x.RunId == second.RunId),
            "StoppedSimulationCannotPublishCandidates"
        );
        Check(
            (await operations.RunAsync("SS", "MyTeam", second.RunId, CancellationToken.None)).Status == "Failed",
            "StoppedRunRetainsFailure"
        );
        await operations.ControlAsync("SS", "MyTeam", "resume", Actor, CancellationToken.None);
        Check(before == await Footprint(db), "OrdinaryAppliedProgressSequenceAuditFootprintUnchanged");
        Check(
            !await db.AuditRecords.AnyAsync(x => x.Id > auditFloor && !x.EntityType.StartsWith("DataMigration")),
            "NoBusinessAuditEscapedSimulation"
        );
        Console.WriteLine(
            JsonSerializer.Serialize(
                new
                {
                    Proof = "ActualPostgresOperations",
                    FirstRun = first.RunId,
                    StoppedRun = second.RunId,
                    Pauses,
                    Applied = false,
                    Result = "SafetyAssertionsPassedNotBusinessAcceptance",
                }
            )
        );
    }

    private static void Check(bool value, string name)
    {
        if (!value)
            throw new InvalidOperationException("Operations PG assertion failed: " + name);
        Console.WriteLine("PG-PASS:" + name);
    }

    private static async Task<string> Footprint(UnifiedDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE OR REPLACE FUNCTION public.ss_operations_footprint() RETURNS text LANGUAGE plpgsql AS $$
            DECLARE t record; value text; items jsonb:='[]'; BEGIN
              FOR t IN SELECT c.relname FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
                WHERE n.nspname='public' AND c.relkind IN ('r','p')
                AND c.relname<>'AuditRecords' AND (c.relname NOT LIKE 'DataMigration%' OR c.relname IN
                  ('DataMigrationRecords','DataMigrationMutations','DataMigrationWorkItems','DataMigrationCheckpoints','DataMigrationWatermarks'))
                ORDER BY c.relname LOOP
                EXECUTE format('SELECT COALESCE(jsonb_agg(jsonb_build_array(ctid::text,xmin::text) ORDER BY ctid),''[]''::jsonb)::text FROM public.%I',t.relname) INTO value;
                items:=items||jsonb_build_array(jsonb_build_array(t.relname,value));
              END LOOP;
              FOR t IN SELECT c.relname FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
                WHERE n.nspname='public' AND c.relkind='S' AND c.relname NOT LIKE 'DataMigration%'
                AND c.relname NOT LIKE 'AuditRecords%' ORDER BY c.relname LOOP
                EXECUTE format('SELECT jsonb_build_array(last_value,log_cnt,is_called)::text FROM public.%I',t.relname) INTO value;
                items:=items||jsonb_build_array(jsonb_build_array(t.relname,value));
              END LOOP;
              IF EXISTS(SELECT 1 FROM pg_namespace WHERE nspname LIKE 'ss_sim_%')
                THEN RAISE EXCEPTION 'Private schema escaped rollback'; END IF;
              RETURN encode(sha256(convert_to(items::text,'UTF8')),'hex');
            END $$;
            """
        );
        return await db
            .Database.SqlQueryRaw<string>("SELECT public.ss_operations_footprint() AS \"Value\"")
            .SingleAsync();
    }
}

internal sealed class RecordingDispatcher(SsMigrationExecutor executor) : IMigrationJobDispatcher
{
    internal Guid? Enqueued { get; private set; }

    public void Validate(string job, bool dryRun) => executor.ValidateRequest(job, dryRun);

    public void Enqueue(Guid runId) => Enqueued = runId;
}
