using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Unified.DataMigration.Options;
using Unified.DataMigration.Sources;
using Unified.Db;
using Unified.Db.Models.DataMigration;

namespace Unified.DataMigration.Services;

// Durable manifests contain only identities/digests. Candidates are reconstructed
// with the real writers in a transaction-private schema, then ALL DDL/DML rolls back.
internal sealed class SsDryRunSimulation(
    UnifiedDbContext db,
    IConfiguration configuration,
    SsSnapshotReader reader,
    SsMyTeamWriter myTeam,
    SsTrainingWriter training,
    SsSchedulingWriter scheduling,
    IEnumerable<ISsMigrationBusinessValidator> validators
)
{
    private static string Q(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";

    private static string L(string value) => "'" + value.Replace("'", "''") + "'";

    private static bool Business(string table) =>
        !table.StartsWith("DataMigration", StringComparison.Ordinal) && table != "AuditRecords";

    internal async Task<(SsWriteSession Session, DataMigrationSimulation? Manifest)> ExecuteAsync(
        DataMigrationRun run,
        SsWriteSession session,
        DataMigrationSourceOptions source,
        int batchSize,
        Func<CancellationToken, Task> fence,
        CancellationToken ct
    )
    {
        var tables = db
            .Model.GetEntityTypes()
            .Where(x => x.GetSchema() is null or "public")
            .Select(x => x.GetTableName())
            .OfType<string>()
            // Obsolete convention model retained by the application after its
            // physical UserLeaves table was removed. Current SS leave uses LeaveEntries.
            .Where(x => x != "UserLeave")
            .Distinct()
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
        // ETL is not an SS writer/read-validation surface. Never clone or write it.
        // Refuse unknown explicit mappings instead of silently falling through to them.
        if (db.Model.GetEntityTypes().Any(x => x.GetSchema() is not (null or "public" or "etl")))
            throw new SsReject("SimulationExplicitSchemaUnsupported");
        var boundary = new DateTimeOffset(
            TimeZoneInfo.ConvertTimeToUtc(
                source.HistoryStartDate.ToDateTime(TimeOnly.MinValue),
                TimeZoneInfo.FindSystemTimeZoneById("America/Vancouver")
            )
        );
        var rows = await reader.ReadAsync(
            SsMyTeamWriter
                .Tables.Concat(SsTrainingWriter.Tables)
                .Concat(SsSchedulingWriter.Tables)
                .Distinct()
                .ToArray(),
            batchSize,
            ct,
            boundary
        );
        var connection = new NpgsqlConnectionStringBuilder(source.ConnectionString);
        var inputHash = SsWriteSession.Hash(
            new
            {
                connection.Host,
                connection.Port,
                connection.Database,
                Rows = rows.OrderBy(x => x.Key, StringComparer.Ordinal)
                    .Select(x => new
                    {
                        Table = x.Key,
                        Rows = x
                            .Value.OrderBy(r => r.Id, StringComparer.Ordinal)
                            .Select(r => r.SimulationInput(x.Key))
                            .ToArray(),
                    })
                    .ToArray(),
            }
        );
        var configHash = SsWriteSession.Hash(
            new
            {
                Version = typeof(SsDryRunSimulation).Assembly.ManifestModule.ModuleVersionId,
                source.CutoverDate,
                source.HistoryStartDate,
                Flags = configuration
                    .AsEnumerable()
                    .Where(x =>
                        x.Key.StartsWith("FeatureFlags:", StringComparison.Ordinal)
                        && x.Key.EndsWith(":Enabled", StringComparison.Ordinal)
                    )
                    .OrderBy(x => x.Key)
                    .ToArray(),
            }
        );
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        DataMigrationSimulation? manifest = null;
        try
        {
            await fence(ct); // Public run-row lock remains held until every simulated business row is gone.
            foreach (var table in tables.Where(Business))
            {
                if (
                    await ScalarAsync($"SELECT COALESCE(to_regclass({L("public." + Q(table))})::text,'missing')", ct)
                    == "missing"
                )
                    throw new SsReject("SimulationMissingBaselineTable:" + table);
                await CommandAsync($"LOCK TABLE public.{Q(table)} IN SHARE MODE", ct);
            }
            var baselineHash = await BaselineAsync(tables, ct);
            var parents = new List<(DataMigrationRun Run, DataMigrationSimulation Manifest)>();
            if (run.Job != "MyTeam")
            {
                var parent = await ParentAsync(run.MyTeamRunId, "MyTeam", null, run, ct);
                parents.Add(parent);
                if (run.Job == "Report")
                    foreach (var job in new[] { "Training", "Scheduling" })
                    {
                        var child = await db
                            .DataMigrationRuns.AsNoTracking()
                            .Where(x =>
                                x.Source == "SS"
                                && x.Job == job
                                && x.DryRun
                                && x.MyTeamRunId == parent.Run.Id
                                && x.Status == "Completed"
                            )
                            .OrderByDescending(x => x.StartedOn)
                            .FirstOrDefaultAsync(ct);
                        parents.Add(await ParentAsync(child?.Id, job, parent.Run.Id, run, ct));
                    }
                if (
                    parents.Any(x =>
                        x.Manifest.SourceHash != inputHash
                        || x.Manifest.BaselineHash != baselineHash
                        || x.Manifest.ConfigurationHash != configHash
                    )
                )
                    throw new SsReject("StaleSimulationInputsOrTargetBaseline");
            }
            var ns =
                parents.Count == 0
                    ? new Guid(
                        SHA256.HashData(Encoding.UTF8.GetBytes($"{inputHash}:{baselineHash}:{configHash}"))[..16]
                    )
                    : parents[0].Manifest.NamespaceId;
            if (parents.Any(x => x.Manifest.NamespaceId != ns))
                throw new SsReject("WrongSimulationNamespace");
            session.SimulationNamespace = ns;
            BoundTracking(session, batchSize, fence);
            var schema = "ss_sim_" + run.Id.ToString("N");
            session.SimulationSchema = schema;
            await CloneAsync(schema, tables, ct);
            await CommandAsync($"SET LOCAL search_path TO {Q(schema)}, pg_catalog", ct);
            db.ChangeTracker.Clear();
            foreach (var parent in parents)
            {
                var hydrated = new SsWriteSession(db, parent.Run)
                {
                    SimulationNamespace = ns,
                    SimulationSchema = schema,
                };
                BoundTracking(hydrated, batchSize, fence);
                await WriteAsync(parent.Run.Job, rows, hydrated, boundary, ct);
                db.ChangeTracker.Clear();
                if (
                    hydrated.Outcomes.Any(x => x.Outcome is "Rejected" or "Failed" or "NeedsAction" or "DroppedBadData")
                )
                    throw new SsReject("SimulationParentRehydrationFailed");
                var receipt = await CandidateAsync(parent.Run.Id, ns, ct);
                if (receipt.Hash != parent.Manifest.CandidateHash || receipt.Count != parent.Manifest.CandidateCount)
                    throw new SsReject("SimulationCandidateReceiptMismatch");
            }
            if (run.Job == "Report")
            {
                await new SsReconciliationWriter(db).WriteAsync(rows, session, ct);
                session.Outcome(
                    "SimulationValidation",
                    run.Id.ToString(),
                    "NeedsAction",
                    "SimulationOnlyNotAppliedLineage"
                );
                // Never invoke an applied-target export or publish from a simulation Report.
            }
            else
            {
                await WriteAsync(run.Job, rows, session, boundary, ct);
                var receipt = await CandidateAsync(run.Id, ns, ct);
                manifest = new DataMigrationSimulation
                {
                    RunId = run.Id,
                    MyTeamRunId = run.Job == "MyTeam" ? run.Id : run.MyTeamRunId!.Value,
                    NamespaceId = ns,
                    SourceHash = inputHash,
                    BaselineHash = baselineHash,
                    ConfigurationHash = configHash,
                    CandidateHash = receipt.Hash,
                    CandidateCount = receipt.Count,
                    Candidates = receipt.Rows,
                };
            }
            session.Outcome(
                "SimulationNamespace",
                ns.ToString(),
                "MigratedWithDifference",
                "RollbackOnlyCandidatesNotAppliedCrosswalks"
            );
        }
        catch (SsReject rejected)
        {
            session.Outcome("DryRunLineage", run.Id.ToString(), "Rejected", rejected.Message);
            manifest = null;
        }
        finally
        {
            await tx.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
        }
        return (session, manifest);
    }

    private async Task<(DataMigrationRun Run, DataMigrationSimulation Manifest)> ParentAsync(
        Guid? id,
        string job,
        Guid? parent,
        DataMigrationRun run,
        CancellationToken ct
    )
    {
        var prior = await db.DataMigrationRuns.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        var manifest = await db.DataMigrationSimulations.AsNoTracking().SingleOrDefaultAsync(x => x.RunId == id, ct);
        if (
            prior is null
            || manifest is null
            || !prior.DryRun
            || prior.Source != "SS"
            || prior.Job != job
            || prior.Status != "Completed"
            || prior.CutoverDate != run.CutoverDate
            || prior.HistoryStartDate != run.HistoryStartDate
            || manifest.FormatVersion != 2
            || manifest.MyTeamRunId != (parent ?? prior.Id)
            || parent is not null && prior.MyTeamRunId != parent
        )
            throw new SsReject("WrongOrMissingSimulationParentLineage");
        var candidates = await db
            .DataMigrationSimulationCandidates.AsNoTracking()
            .Where(x => x.RunId == prior.Id)
            .ToListAsync(ct);
        if (
            candidates.Count != manifest.CandidateCount
            || candidates.Any(x => x.Source != "SS" || x.NamespaceId != manifest.NamespaceId)
            || CandidateHash(candidates) != manifest.CandidateHash
        )
            throw new SsReject("WrongOrMissingSimulationCandidateNamespace");
        return (prior, manifest);
    }

    private void BoundTracking(SsWriteSession session, int batchSize, Func<CancellationToken, Task> fence)
    {
        var units = 0;
        session.AfterUnitAsync = async token =>
        {
            token.ThrowIfCancellationRequested();
            if (++units >= batchSize)
            {
                // RowAsync/AtomicGraphAsync have saved the entire unit before this
                // callback. Detaching is not a commit: the outer transaction still
                // owns every simulated row, identity sequence and audit mutation.
                db.ChangeTracker.Clear();
                units = 0;
                await fence(token);
            }
        };
    }

    private async Task WriteAsync(
        string job,
        Dictionary<string, List<SsRow>> rows,
        SsWriteSession session,
        DateTimeOffset boundary,
        CancellationToken ct
    )
    {
        if (job == "MyTeam")
            await myTeam.WriteAsync(rows, session, boundary, ct);
        else if (job == "Training")
            await training.WriteAsync(rows, session, boundary, ct);
        else if (job == "Scheduling")
        {
            await scheduling.WriteAsync(rows, session, boundary, ct);
            var checks = validators.ToList();
            if (checks.Count == 0)
                session.Outcome(
                    "SchedulingValidation",
                    session.Run.Id.ToString(),
                    "NeedsAction",
                    "ActualSchedulingReadValidatorUnavailable"
                );
            foreach (var validator in checks)
                await validator.ValidateAsync(job, rows, session, ct);
        }
        else
            throw new SsReject("UnsupportedSimulationJob");
    }

    private static string CandidateHash(IEnumerable<DataMigrationSimulationCandidate> rows) =>
        SsWriteSession.Hash(
            rows.OrderBy(x => x.EntityType, StringComparer.Ordinal)
                .ThenBy(x => x.LegacyId, StringComparer.Ordinal)
                .Select(x => new
                {
                    x.EntityType,
                    x.LegacyId,
                    x.TargetType,
                    x.TargetKey,
                    x.PayloadHash,
                })
                .ToArray()
        );

    private async Task<(string Hash, int Count, List<DataMigrationSimulationCandidate> Rows)> CandidateAsync(
        Guid runId,
        Guid ns,
        CancellationToken ct
    )
    {
        var records = await db
            .DataMigrationRecords.AsNoTracking()
            .Where(x => x.LastAppliedRunId == runId)
            .Select(x => new DataMigrationSimulationCandidate
            {
                RunId = runId,
                NamespaceId = ns,
                EntityType = x.EntityType,
                LegacyId = x.LegacyId,
                TargetType = x.TargetType,
                TargetKey = x.TargetKey,
                PayloadHash = x.PayloadHash,
            })
            .ToListAsync(ct);
        return (CandidateHash(records), records.Count, records);
    }

    private async Task<string> BaselineAsync(string[] tables, CancellationToken ct)
    {
        var hashes = new List<object>();
        foreach (var table in tables.Where(Business))
        {
            var metadata = db.Model.GetEntityTypes().First(x => x.GetTableName() == table);
            var store = Microsoft.EntityFrameworkCore.Metadata.StoreObjectIdentifier.Table(table, metadata.GetSchema());
            var keys =
                metadata.FindPrimaryKey()?.Properties.Select(x => "t." + Q(x.GetColumnName(store)!)).ToArray()
                ?? throw new SsReject("SimulationUnkeyedTableUnsupported");
            var json =
                table == "Users"
                    ? "(to_jsonb(t)-'Photo') || jsonb_build_object('_photoRowVersion',t.xmin::text)"
                    : "to_jsonb(t)";
            var digest = await ScalarAsync(
                $"""
                SELECT encode(sha256(convert_to(COALESCE(jsonb_agg({json}
                ORDER BY jsonb_build_array({string.Join(",", keys)})), '[]'::jsonb)::text,'UTF8')),'hex')
                FROM public.{Q(table)} t
                """,
                ct
            );
            var structure = await ScalarAsync(
                $"""
                SELECT encode(sha256(convert_to(jsonb_build_object(
                  'columns',(SELECT jsonb_agg(jsonb_build_array(a.attname,format_type(a.atttypid,a.atttypmod),
                    a.attnotnull,a.attidentity,a.attgenerated,pg_get_expr(d.adbin,d.adrelid)) ORDER BY a.attnum)
                    FROM pg_attribute a LEFT JOIN pg_attrdef d ON d.adrelid=a.attrelid AND d.adnum=a.attnum
                    WHERE a.attrelid={L("public." + Q(table))}::regclass AND a.attnum>0 AND NOT a.attisdropped),
                  'constraints',(SELECT jsonb_agg(pg_get_constraintdef(k.oid,true) ORDER BY k.conname)
                    FROM pg_constraint k WHERE k.conrelid={L("public." + Q(table))}::regclass)
                  )::text,'UTF8')),'hex')
                """,
                ct
            );
            hashes.Add(
                new
                {
                    Table = table,
                    Hash = digest,
                    Structure = structure,
                }
            );
        }
        return SsWriteSession.Hash(hashes);
    }

    private async Task<string> ScalarAsync(string sql, CancellationToken ct)
    {
        await using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        cmd.CommandText = sql;
        return (string)(await cmd.ExecuteScalarAsync(ct))!;
    }

    private async Task CommandAsync(string sql, CancellationToken ct)
    {
        await using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task CloneAsync(string schema, string[] tables, CancellationToken ct)
    {
        // Names come exclusively from the EF model and a generated run UUID.
        // No public fallback, applied crosswalks, public sequences, keyrings or
        // source payload/audit copies are used. FKs point ONLY inside the scratch schema.
        var names = string.Join(",", tables.Select(L));
        await CommandAsync(
            $"""
            CREATE SCHEMA {Q(schema)};
            DO $simulation$
            DECLARE t text; cols text; item record; seq text; value bigint; ddl text; phase text := 'inventory';
            BEGIN
              IF EXISTS(SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
                WHERE n.nspname='public' AND c.relname IN ({names}) AND c.relrowsecurity)
              THEN RAISE EXCEPTION 'Unsupported row security in simulation'; END IF;
              IF EXISTS(SELECT 1 FROM pg_trigger g JOIN pg_class c ON c.oid=g.tgrelid
                JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='public'
                AND c.relname IN ({names}) AND NOT g.tgisinternal
                AND g.tgname NOT IN ('ss_journal_immutable','ss_rollback_capture'))
              THEN RAISE EXCEPTION 'Unsupported business trigger in simulation'; END IF;
              IF EXISTS(SELECT 1 FROM pg_constraint k JOIN pg_class c ON c.oid=k.conrelid
                JOIN pg_namespace n ON n.oid=c.relnamespace JOIN pg_class p ON p.oid=k.confrelid
                JOIN pg_namespace pn ON pn.oid=p.relnamespace
                WHERE k.contype='f' AND n.nspname='public' AND c.relname IN ({names})
                AND (pn.nspname<>'public' OR p.relname NOT IN ({names})))
              THEN RAISE EXCEPTION 'Unsupported external FK in simulation'; END IF;
              FOREACH t IN ARRAY ARRAY[{names}] LOOP
                phase:='create';
                EXECUTE format('CREATE TABLE %I.%I (LIKE public.%I INCLUDING ALL)',{L(schema)},t,t);
                SELECT string_agg(quote_ident(attname),',' ORDER BY attnum) INTO cols
                  FROM pg_attribute WHERE attrelid=format('public.%I',t)::regclass AND attnum>0 AND NOT attisdropped AND attgenerated='';
                IF t NOT LIKE 'DataMigration%' OR t IN ('DataMigrationRuns','DataMigrationOutcomes','DataMigrationControls') THEN
                  IF t<>'AuditRecords' THEN
                    phase:='copy';
                    EXECUTE format('INSERT INTO %I.%I (%s) OVERRIDING SYSTEM VALUE SELECT %s FROM public.%I',
                      {L(schema)},t,cols,cols,t);
                  END IF;
                END IF;
                FOR item IN SELECT a.attname,a.attidentity,pg_get_expr(d.adbin,d.adrelid) AS def
                  FROM pg_attribute a LEFT JOIN pg_attrdef d ON d.adrelid=a.attrelid AND d.adnum=a.attnum
                  WHERE a.attrelid=format('public.%I',t)::regclass AND a.attnum>0 AND NOT a.attisdropped LOOP
                  phase:='sequence';
                  IF item.attidentity<>'' THEN
                    seq:=pg_get_serial_sequence(format('%I.%I',{L(schema)},t),item.attname);
                  ELSIF item.def LIKE 'nextval(%' THEN
                    seq:=format('%I.%I',{L(schema)},'sim_seq_'||t||'_'||item.attname);
                    EXECUTE 'CREATE SEQUENCE '||seq;
                    EXECUTE format('ALTER TABLE %I.%I ALTER COLUMN %I SET DEFAULT nextval(%L::regclass)',{L(
                schema
            )},t,item.attname,seq);
                  ELSE CONTINUE; END IF;
                  EXECUTE format('SELECT COALESCE(max(%I),0)+1 FROM %I.%I',item.attname,{L(schema)},t) INTO value;
                  PERFORM setval(seq::regclass,GREATEST(value,1),false);
                END LOOP;
              END LOOP;
              FOR item IN SELECT k.conname,ch.relname,pg_get_constraintdef(k.oid,true) AS def
                FROM pg_constraint k JOIN pg_class ch ON ch.oid=k.conrelid JOIN pg_namespace n ON n.oid=ch.relnamespace
                WHERE k.contype='f' AND n.nspname='public' AND ch.relname IN ({names}) LOOP
                phase:='foreign-key';
                ddl:=replace(replace(item.def,'REFERENCES public.','REFERENCES '),'REFERENCES ','REFERENCES {Q(schema)}.');
                EXECUTE format('ALTER TABLE %I.%I ADD CONSTRAINT %I %s',{L(schema)},item.relname,item.conname,ddl);
              END LOOP;
            EXCEPTION WHEN OTHERS THEN
              RAISE EXCEPTION USING ERRCODE=SQLSTATE,MESSAGE='Simulation schema clone failed',
                SCHEMA={L(schema)},TABLE=COALESCE(t,'metadata'),HINT=phase;
            END $simulation$;
            """,
            ct
        );
    }
}
