using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Npgsql;
using NpgsqlTypes;
using Unified.DataMigration.Options;
using Unified.Db;
using Unified.Db.Models.DataMigration;

namespace Unified.DataMigration.Services;

// Explicit migration-only administrative operation; never registered as an automatic job.
public sealed class SsRollbackService(UnifiedDbContext db, SsRollbackJournal crypto, IConfiguration configuration)
{
    private sealed record Table(string Schema, string Name, string[] Keys)
    {
        public string Sql => $"{Quote(Schema)}.{Quote(Name)}";
        public string Id => Schema + "." + Name;
    }

    private sealed record Row(Table Table, string Key, JsonObject? Values, string Hash, string Version, bool HasPhoto)
    {
        public string Id => Table.Id + ":" + Key;
    }

    private sealed record Undo(
        DataMigrationJournal First,
        DataMigrationJournal Last,
        Row Current,
        JsonObject? Before,
        string[] Columns,
        bool PhotoChanged
    );

    private sealed record Plan(
        DataMigrationRun Run,
        List<Undo> Undo,
        List<Row> Deletes,
        List<Row> ExtraDependents,
        string[] Blockers,
        string Digest
    );

    private sealed record Relation(string Child, string Parent, string[] Columns, string[] PrincipalColumns);

    private Dictionary<string, Table>? catalog;
    private List<Relation> relations = [];
    private bool AuthorizedRehearsal =>
        configuration.GetValue<bool>("DataMigration:Sources:SS:AllowUnrecoverableSyntheticApply")
        && db.Database.GetDbConnection().Database
            is "unified_ss_rehearsal"
                or "unified_ss_rehearsal_ready"
                or "unified_ss_rehearsal_ready_v2"
                or "unified_ss_rehearsal_ready_v3"
                or "unified_ss_rehearsal_ready_v3_drift"
                or "unified_ss_rehearsal_ready_v4"
                or "unified_ss_rehearsal_ready_v4_recovery"
                or "unified_ss_rehearsal_ready_v4_probe"
                or "unified_ss_rehearsal_ready_v4_rollback"
                or "unified_ss_rehearsal_repartition_v4"
                or "unified_ss_rehearsal_repartition_v5"
                or "unified_ss_rehearsal_repartition_history_v5";

    private Dictionary<string, Table> Tables =>
        catalog
        ?? db.Model.GetEntityTypes()
            .Where(x => x.GetTableName() != null && x.FindPrimaryKey() != null)
            .GroupBy(x => (Schema: x.GetSchema() ?? "public", Name: x.GetTableName()!))
            .ToDictionary(
                g => g.Key.Schema + "." + g.Key.Name,
                g =>
                {
                    var e = g.First();
                    var store = StoreObjectIdentifier.Table(g.Key.Name, e.GetSchema());
                    return new Table(
                        g.Key.Schema,
                        g.Key.Name,
                        e.FindPrimaryKey()!.Properties.Select(x => x.GetColumnName(store)!).ToArray()
                    );
                }
            );

    public async Task<(string Digest, string Export, int Rows, int Dependents, string[] Blockers)> PreviewAsync(
        Guid runId,
        CancellationToken ct
    )
    {
        ConfigureMigrationCommandTimeout();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var plan = await BuildPlanAsync(runId, ct);
        var export = await ExportAsync(plan, ct);
        await tx.RollbackAsync(ct);
        return (plan.Digest, export, plan.Undo.Count, plan.ExtraDependents.Count, plan.Blockers);
    }

    public async Task<Guid> ApplyAsync(Guid runId, string confirmation, CancellationToken ct)
    {
        ConfigureMigrationCommandTimeout();
        try
        {
            return await ApplyCoreAsync(runId, confirmation, ct);
        }
        catch (Exception error)
        {
            // ApplyCore's transaction is disposed/rolled back BEFORE retaining failure.
            db.ChangeTracker.Clear();
            if (
                AuthorizedRehearsal
                && await db.DataMigrationRuns.AnyAsync(x => x.Id == runId && x.Source == "SS", CancellationToken.None)
            )
            {
                db.DataMigrationFailures.Add(
                    new DataMigrationFailure
                    {
                        RunId = runId,
                        Source = "SS",
                        ErrorType = "RollbackFailed",
                        ErrorMessage =
                            "SS inverse did not commit; previous applied data and journal remain. FailureKind="
                            + error.GetType().Name
                            + (error is PostgresException pg ? ";SqlState=" + pg.SqlState : ""),
                        OccurredOn = DateTimeOffset.UtcNow,
                    }
                );
                await db.SaveChangesAsync(CancellationToken.None);
            }
            throw;
        }
    }

    // Sensitive values are available ONLY through the separately permissioned impact endpoint.
    // Rebuild/revalidate the exact run-bound plan; never accept an arbitrary server file path.
    public async Task<byte[]> ReadImpactAsync(Guid runId, string digest, CancellationToken ct)
    {
        ConfigureMigrationCommandTimeout();
        if (!DataMigrationOperations.ValidDigest(digest))
            throw new InvalidOperationException("Invalid rollback impact digest.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var plan = await BuildPlanAsync(runId, ct);
        if (plan.Digest != digest)
            throw new InvalidOperationException("Rollback impact changed; preview again.");
        var path = ExportPath(plan);
        if (!File.Exists(path))
            throw new InvalidOperationException("Restricted impact preview is required.");
        ValidateExportFile(path);
        var json = await File.ReadAllTextAsync(path, ct);
        if (json != ExportJson(plan))
            throw new InvalidOperationException("Immutable rollback impact export mismatch.");
        await tx.RollbackAsync(ct);
        return Encoding.UTF8.GetBytes(json);
    }

    private async Task<Guid> ApplyCoreAsync(Guid runId, string confirmation, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var plan = await BuildPlanAsync(runId, ct);
        if (plan.Blockers.Length != 0)
            throw new InvalidOperationException("SS rollback is blocked; inspect restricted preview.");
        var expected = $"{runId:D}:{plan.Digest}";
        if (
            !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(confirmation),
                Encoding.UTF8.GetBytes(expected)
            )
        )
            throw new InvalidOperationException("ConfirmDataLoss must match this run and its current preview digest.");
        // Require the exact restricted impact export to have been created before confirmation.
        var export = ExportPath(plan);
        if (File.Exists(export))
            ValidateExportFile(export);
        if (!File.Exists(export) || await File.ReadAllTextAsync(export, ct) != ExportJson(plan))
            throw new InvalidOperationException("SS rollback requires the unchanged restricted impact export.");
        // Never journal the inverse into the original run; evidence is immutable.
        await db.Database.ExecuteSqlRawAsync("SELECT set_config('unified_ss.run','',true)", ct);
        var original = plan.Undo.Where(x => x.First.Operation != "I").ToList();
        // Replacement auxiliary rows may occupy the deleted originals' unique keys
        // (e.g. ShiftEntryId + AssignmentEntryId). Remove confirmed created leaf
        // rows before restoration, not after inserting those originals. A surviving
        // original FK dependent prevents early deletion until its FK is restored.
        // No constraints are disabled and every operation remains in this inverse TX.
        foreach (var row in plan.Deletes)
            if ((await ChildrenAsync(row, ct)).Count == 0)
                await CommandAsync(
                    $"DELETE FROM {row.Table.Sql} t WHERE to_jsonb(t) @> @key::jsonb",
                    ct,
                    new NpgsqlParameter("key", NpgsqlDbType.Jsonb) { Value = row.Key }
                );
        // Free migration-created natural-key identities before restoring originals.
        // PKs/FKs never change; these rows are all removed in this same transaction.
        foreach (
            var row in plan.Deletes.Where(x => plan.Undo.Any(u => u.First.Operation == "I" && u.Current.Id == x.Id))
        )
        {
            if (row.Table.Name == "AssignmentDefinitions")
            {
                var name = "__ss_undo_" + Guid.NewGuid().ToString("N");
                await CommandAsync(
                    $"UPDATE {row.Table.Sql} t SET \"Name\"=@name,\"NormalizedName\"=@name WHERE to_jsonb(t) @> @key::jsonb",
                    ct,
                    new NpgsqlParameter("name", name),
                    new NpgsqlParameter("key", NpgsqlDbType.Jsonb) { Value = row.Key }
                );
            }
            if (row.Table.Name == "Users")
                await CommandAsync(
                    $"UPDATE {row.Table.Sql} t SET \"IdirName\"=@name,\"IdirId\"=NULL,\"KeyCloakId\"=NULL WHERE to_jsonb(t) @> @key::jsonb",
                    ct,
                    new NpgsqlParameter("name", "ssrb" + Guid.NewGuid().ToString("N")[..12]),
                    new NpgsqlParameter("key", NpgsqlDbType.Jsonb) { Value = row.Key }
                );
        }
        // Restore deleted originals in FK order. All rows are read/locked under the same plan.
        var missing = original.Where(x => x.Current.Values is null).ToDictionary(x => x.Current.Id);
        while (missing.Count > 0)
        {
            var restored = false;
            foreach (var item in missing.Values.OrderByDescending(x => x.First.Id).ToList())
            {
                if (HasMissingParent(item, missing.Values))
                    continue;
                await RestoreAsync(item, true, ct);
                missing.Remove(item.Current.Id);
                restored = true;
            }
            if (!restored)
                throw new InvalidOperationException("SS rollback cannot restore a cyclic original FK graph.");
        }
        foreach (var item in original.Where(x => x.Current.Values is not null).OrderByDescending(x => x.First.Id))
            await RestoreAsync(item, false, ct);
        // Child-first order covers both every migration-created auxiliary row and
        // later application-created dependents explicitly included in the impact.
        foreach (var row in plan.Deletes)
            await CommandAsync(
                $"DELETE FROM {row.Table.Sql} t WHERE to_jsonb(t) @> @key::jsonb",
                ct,
                new NpgsqlParameter("key", NpgsqlDbType.Jsonb) { Value = row.Key }
            );
        db.ChangeTracker.Clear();
        foreach (var row in plan.Deletes)
            if ((await ReadAsync(row.Table, row.Key, ct)).Values is not null)
                throw new InvalidOperationException("SS rollback delete footprint verification failed.");
        foreach (var item in original)
        {
            var current = await ReadAsync(item.Current.Table, item.Current.Key, ct);
            var columns = item.First.Operation == "D" ? item.Before!.Select(x => x.Key) : item.Columns;
            if (
                current.Values is null
                || columns.Any(col => col != "Photo" && !JsonNode.DeepEquals(current.Values[col], item.Before![col]))
            )
                throw new InvalidOperationException("SS rollback original-value footprint verification failed.");
            if (item.PhotoChanged && !item.First.BeforeHadPhoto && current.HasPhoto)
                throw new InvalidOperationException("SS rollback null-photo footprint verification failed.");
        }
        var receipt = new DataMigrationRun
        {
            Source = "SS",
            Job = "Rollback",
            DryRun = false,
            Status = "Completed",
            StartedOn = DateTimeOffset.UtcNow,
            CompletedOn = DateTimeOffset.UtcNow,
            RollbackOfRunId = runId,
            CutoverDate = plan.Run.CutoverDate,
            ScannedCount = plan.Undo.Count,
            UpdatedCount = original.Count,
            SkippedCount = plan.ExtraDependents.Count,
        };
        db.DataMigrationRuns.Add(receipt);
        await db.SaveChangesAsync(ct);
        // Restoring values changes xmin. Refresh only receipts whose original mapped
        // digest is proven restored, never silently bless unrelated application edits.
        var trusted = plan
            .Undo.Where(x =>
                !x.Current.HasPhoto && !x.First.BeforeHadPhoto
                || x.Current.Hash == x.Last.AfterHash && x.Current.Version == x.Last.AfterVersion
            )
            .ToList();
        var targets = db
            .Model.GetEntityTypes()
            .Where(x => x.GetTableName() != null && x.FindPrimaryKey()?.Properties.Count == 1)
            .SelectMany(e =>
                trusted
                    .Where(x => x.Current.Table.Id == (e.GetSchema() ?? "public") + "." + e.GetTableName())
                    .Select(x => (e.ClrType.Name, JsonNode.Parse(x.Current.Key)!.AsObject().Single().Value!.ToString()))
            )
            .ToArray();
        var session = new SsWriteSession(db, receipt);
        await session.RefreshRestoredReceiptsAsync(targets, ct);
        var run = await db.DataMigrationRuns.SingleAsync(x => x.Id == runId, ct);
        run.Status = "RolledBack";
        db.DataMigrationOutcomes.Add(
            new DataMigrationOutcome
            {
                RunId = receipt.Id,
                EntityType = "RollbackProof",
                LegacyId = runId.ToString(),
                Outcome = "Migrated",
                Reason =
                    $"AtomicInverseVerified:Restored={original.Count}:Deleted={plan.Deletes.Count}:LaterDependents={plan.ExtraDependents.Count}",
            }
        );
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return receipt.Id;
    }

    private async Task<Plan> BuildPlanAsync(Guid runId, CancellationToken ct)
    {
        if (!AuthorizedRehearsal)
            throw new InvalidOperationException("SS rollback apply/preview remains isolated-rehearsal only.");
        _ = crypto.Key;
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(872,1)", ct);
        var catalogJson = await ScalarAsync(
            """
            SELECT coalesce(jsonb_agg(jsonb_build_object('Schema',n.nspname,'Name',c.relname,'Keys',k.cols)),'[]'::jsonb)::text
            FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
            JOIN LATERAL (
              SELECT jsonb_agg(a.attname ORDER BY p.ord) AS cols
              FROM pg_index i CROSS JOIN LATERAL unnest(i.indkey) WITH ORDINALITY p(num,ord)
              JOIN pg_attribute a ON a.attrelid=c.oid AND a.attnum=p.num
              WHERE i.indrelid=c.oid AND i.indisprimary
            ) k ON k.cols IS NOT NULL
            WHERE c.relkind IN ('r','p') AND n.nspname NOT LIKE 'pg_%' AND n.nspname<>'information_schema'
            """,
            ct
        );
        catalog = JsonSerializer.Deserialize<List<Table>>(catalogJson!)!.ToDictionary(x => x.Id);
        var relationJson = await ScalarAsync(
            """
            SELECT coalesce(jsonb_agg(jsonb_build_object(
              'Child',cn.nspname||'.'||c.relname,'Parent',pn.nspname||'.'||p.relname,
              'Columns', (SELECT jsonb_agg(a.attname ORDER BY k.ord)
                FROM unnest(f.conkey) WITH ORDINALITY k(num,ord)
                JOIN pg_attribute a ON a.attrelid=c.oid AND a.attnum=k.num),
              'PrincipalColumns', (SELECT jsonb_agg(a.attname ORDER BY k.ord)
                FROM unnest(f.confkey) WITH ORDINALITY k(num,ord)
                JOIN pg_attribute a ON a.attrelid=p.oid AND a.attnum=k.num)
            )),'[]'::jsonb)::text
            FROM pg_constraint f JOIN pg_class c ON c.oid=f.conrelid
            JOIN pg_class p ON p.oid=f.confrelid
            JOIN pg_namespace cn ON cn.oid=c.relnamespace JOIN pg_namespace pn ON pn.oid=p.relnamespace
            WHERE f.contype='f'
            """,
            ct
        );
        relations = JsonSerializer.Deserialize<List<Relation>>(relationJson!)!;
        var runs = await db.DataMigrationRuns.AsNoTracking().Where(x => x.Source == "SS").ToListAsync(ct);
        var run =
            runs.SingleOrDefault(x => x.Id == runId)
            ?? throw new InvalidOperationException("SS rollback run was not found.");
        // Lock all mapped tables so FK discovery and digest revalidation also fence ordinary writers.
        foreach (var table in Tables.Values.OrderBy(x => x.Id, StringComparer.Ordinal))
            await CommandAsync($"LOCK TABLE {table.Sql} IN SHARE ROW EXCLUSIVE MODE", ct);
        var blockers = new List<string>();
        if (relations.Any(x => Tables.ContainsKey(x.Parent) && !Tables.ContainsKey(x.Child)))
            blockers.Add("UnkeyedDependentTableRequiresExplicitResolution");
        if (runs.Any(x => x.Status == "Running"))
            blockers.Add("SourceJobRunning");
        if (run.DryRun || run.Status == "RolledBack")
            blockers.Add("RunNotApplied");
        if (run.Job != "Report" && run.RollbackJournalKeyId is null)
            blockers.Add("MissingOriginalRollbackJournal");
        if (run.RollbackJournalKeyId is { } kid && kid != crypto.KeyId)
            blockers.Add("OriginalRollbackKeyRequired");
        var live = runs.Where(x => x.Id != run.Id && !x.DryRun && x.Status != "RolledBack" && x.Job != "Rollback")
            .ToList();
        if (live.Any(x => x.Job == run.Job && x.StartedOn > run.StartedOn))
            blockers.Add("LaterSameJobRunMustRollbackFirst");
        if (
            run.Job == "MyTeam"
            && live.Any(x => x.MyTeamRunId == run.Id && x.Job is "Training" or "Scheduling" or "Report")
        )
            blockers.Add("DependentJobsMustRollbackFirst");
        if (
            run.Job is "Training" or "Scheduling"
            && live.Any(x => x.Job == "Report" && x.MyTeamRunId == run.MyTeamRunId && x.StartedOn >= run.StartedOn)
        )
            blockers.Add("ReportMustRollbackFirst");
        var journal = await db
            .DataMigrationJournals.AsNoTracking()
            .Where(x => x.RunId == runId)
            .OrderBy(x => x.Id)
            .ToListAsync(ct);
        var undo = new List<Undo>();
        foreach (var group in journal.GroupBy(x => x.SchemaName + "." + x.TableName + ":" + CanonicalKey(x.RowKey)))
        {
            var first = group.First();
            var last = group.Last();
            var table = Tables[first.SchemaName + "." + first.TableName];
            var current = await ReadAsync(table, CanonicalKey(first.RowKey), ct);
            JsonObject? before = null;
            if (first.BeforeImage is not null && first.KeyId == crypto.KeyId)
            {
                var json = await ScalarAsync(
                    "SELECT pgp_sym_decrypt(@image,@secret)",
                    ct,
                    new NpgsqlParameter("image", first.BeforeImage),
                    new NpgsqlParameter("secret", crypto.Key)
                );
                before = JsonNode.Parse(json!)!.AsObject();
            }
            if (first.Operation != "I" && before is null)
                blockers.Add("OriginalBeforeImageUnavailable");
            var photoChanged = group.Any(x => x.PhotoChanged);
            if (first.Operation != "I" && first.BeforeHadPhoto && (photoChanged || current.Values is null))
                blockers.Add("PreviousPrivatePhotoCannotBeRestored");
            undo.Add(
                new Undo(
                    first,
                    last,
                    current,
                    before,
                    group
                        .SelectMany(x => x.ChangedColumns)
                        .Distinct()
                        .OrderBy(x => x, StringComparer.Ordinal)
                        .ToArray(),
                    photoChanged
                )
            );
        }
        var roots = undo.Where(x => x.First.Operation == "I" && x.Current.Values is not null)
            .Select(x => x.Current)
            .ToList();
        var planned = roots.ToDictionary(x => x.Id);
        var extra = new Dictionary<string, Row>();
        var ordered = new List<Row>();
        var visited = new HashSet<string>();
        var active = new HashSet<string>();
        async Task Visit(Row row)
        {
            if (visited.Contains(row.Id))
                return;
            if (!active.Add(row.Id))
            {
                blockers.Add("CyclicDeleteDependencyNeedsResolution");
                return;
            }
            foreach (var child in (await ChildrenAsync(row, ct)).OrderBy(x => x.Id, StringComparer.Ordinal))
            {
                if (!planned.ContainsKey(child.Id))
                    extra[child.Id] = child;
                await Visit(child);
            }
            active.Remove(row.Id);
            visited.Add(row.Id);
            ordered.Add(row);
        }
        foreach (var root in roots.OrderBy(x => x.Id, StringComparer.Ordinal))
            await Visit(root);
        // An existing row restored by this run cannot also be cascaded away as "later data".
        if (undo.Any(x => x.First.Operation != "I" && extra.ContainsKey(x.Current.Id)))
            blockers.Add("RestoredRowAlsoDependsOnDeletedTarget");
        var binding = new
        {
            Version = 1,
            Database = db.Database.GetDbConnection().Database,
            Run = run.Id,
            run.Job,
            run.Status,
            run.RollbackJournalKeyId,
            Journals = journal.Select(x => new
            {
                x.Id,
                x.KeyId,
                x.AfterHash,
                x.AfterVersion,
            }),
            Rows = undo.OrderBy(x => x.Current.Id, StringComparer.Ordinal)
                .Select(x => new
                {
                    x.Current.Id,
                    x.Current.Hash,
                    x.Current.Version,
                }),
            Dependents = extra
                .Values.OrderBy(x => x.Id, StringComparer.Ordinal)
                .Select(x => new
                {
                    x.Id,
                    x.Hash,
                    x.Version,
                }),
            Dependencies = live.OrderBy(x => x.Id).Select(x => new { x.Id, x.Status }),
            Blockers = blockers.Distinct().OrderBy(x => x, StringComparer.Ordinal),
        };
        var digest = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(binding)));
        return new Plan(
            run,
            undo,
            ordered,
            extra.Values.OrderBy(x => x.Id, StringComparer.Ordinal).ToList(),
            blockers.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            digest
        );
    }

    private bool HasMissingParent(Undo item, IEnumerable<Undo> remaining)
    {
        foreach (var fk in relations.Where(x => x.Child == item.Current.Table.Id))
        foreach (var parent in remaining.Where(x => x.Current.Id != item.Current.Id && x.Current.Table.Id == fk.Parent))
            if (
                fk
                    .Columns.Select(
                        (col, i) =>
                            item.Before![col] is not null
                            && JsonNode.DeepEquals(item.Before[col], parent.Before![fk.PrincipalColumns[i]])
                    )
                    .All(x => x)
            )
                return true;
        return false;
    }

    private async Task<List<Row>> ChildrenAsync(Row parent, CancellationToken ct)
    {
        var result = new Dictionary<string, Row>();
        foreach (var fk in relations.Where(x => x.Parent == parent.Table.Id))
        {
            if (!Tables.TryGetValue(fk.Child, out var table))
                continue;
            var join = string.Join(
                " AND ",
                fk.Columns.Select((col, i) => $"c.{Quote(col)}=p.{Quote(fk.PrincipalColumns[i])}")
            );
            await using var command = CreateCommand(
                $"SELECT (to_jsonb(c)-'Photo')::text,c.xmin::text FROM {table.Sql} c JOIN {parent.Table.Sql} p ON {join} WHERE to_jsonb(p) @> @key::jsonb",
                new NpgsqlParameter("key", NpgsqlDbType.Jsonb) { Value = parent.Key }
            );
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var values = JsonNode.Parse(reader.GetString(0))!.AsObject();
                var key = KeyFrom(table, values);
                // Version binds private-photo changes too; no private-photo hash/export.
                var row = new Row(table, key, values, SsWriteSession.Hash(values), reader.GetString(1), false);
                result[row.Id] = row;
            }
        }
        return result.Values.ToList();
    }

    private async Task<Row> ReadAsync(Table table, string key, CancellationToken ct)
    {
        await using var command = CreateCommand(
            $"SELECT (to_jsonb(t)-'Photo')::text,encode(digest((to_jsonb(t)-'Photo')::text,'sha256'),'hex'),t.xmin::text,(to_jsonb(t)->'Photo' IS NOT NULL AND to_jsonb(t)->'Photo'<>'null'::jsonb) FROM {table.Sql} t WHERE to_jsonb(t) @> @key::jsonb",
            new NpgsqlParameter("key", NpgsqlDbType.Jsonb) { Value = key }
        );
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return new Row(table, key, null, "missing", "missing", false);
        return new Row(
            table,
            key,
            JsonNode.Parse(reader.GetString(0))!.AsObject(),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetBoolean(3)
        );
    }

    private async Task RestoreAsync(Undo item, bool insert, CancellationToken ct)
    {
        var table = item.Current.Table;
        var columns = (insert ? item.Before!.Select(x => x.Key) : item.Columns)
            .Where(x => !table.Keys.Contains(x) || insert)
            .ToArray();
        if (item.PhotoChanged && !item.First.BeforeHadPhoto && !columns.Contains("Photo"))
            columns = [.. columns, "Photo"];
        if (columns.Length == 0)
            return;
        var before = item.Before!.ToJsonString();
        var cols = string.Join(",", columns.Select(Quote));
        var selected = string.Join(",", columns.Select(x => "v." + Quote(x)));
        var sql = insert
            ? $"INSERT INTO {table.Sql} ({cols}) OVERRIDING SYSTEM VALUE SELECT {selected} FROM jsonb_populate_record(NULL::{table.Sql},@before::jsonb) v"
            : $"UPDATE {table.Sql} t SET {string.Join(",", columns.Select(x => $"{Quote(x)}=v.{Quote(x)}"))} FROM jsonb_populate_record(NULL::{table.Sql},@before::jsonb) v WHERE to_jsonb(t) @> @key::jsonb";
        await CommandAsync(
            sql,
            ct,
            new NpgsqlParameter("before", NpgsqlDbType.Jsonb) { Value = before },
            new NpgsqlParameter("key", NpgsqlDbType.Jsonb) { Value = item.Current.Key }
        );
    }

    private string ExportPath(Plan plan)
    {
        var directory = configuration["DataMigrationRollback:ExportDirectory"] ?? "/restricted";
        if (!OperatingSystem.IsLinux() || !Path.IsPathFullyQualified(directory))
            throw new InvalidOperationException(
                "Restricted rollback exports require a dedicated Linux private volume."
            );
        Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        if (
            (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0
            || (
                File.GetUnixFileMode(directory)
                & (
                    UnixFileMode.GroupRead
                    | UnixFileMode.GroupWrite
                    | UnixFileMode.GroupExecute
                    | UnixFileMode.OtherRead
                    | UnixFileMode.OtherWrite
                    | UnixFileMode.OtherExecute
                )
            ) != 0
        )
            throw new InvalidOperationException("Rollback export directory must be owner-only, not linked.");
        return Path.Combine(directory, $"ss-rollback-{plan.Run.Id:D}-{plan.Digest}.json");
    }

    private static string ExportJson(Plan plan) =>
        JsonSerializer.Serialize(
            new
            {
                RunId = plan.Run.Id,
                plan.Digest,
                ConfirmDataLoss = $"{plan.Run.Id:D}:{plan.Digest}",
                plan.Blockers,
                Rows = plan.Undo.Select(x => new
                {
                    Table = x.Current.Table.Id,
                    Key = x.Current.Key,
                    x.First.Operation,
                    x.Current.Values,
                    x.Current.HasPhoto,
                    LaterEdit = x.Current.Hash != x.Last.AfterHash || x.Current.Version != x.Last.AfterVersion,
                    ChangedColumns = x.Columns,
                }),
                LaterDependents = plan.ExtraDependents.Select(x => new
                {
                    Table = x.Table.Id,
                    x.Key,
                    x.Values,
                }),
                PhotosIncluded = false,
            },
            new JsonSerializerOptions { WriteIndented = true }
        );

    private async Task<string> ExportAsync(Plan plan, CancellationToken ct)
    {
        if (!OperatingSystem.IsLinux())
            throw new InvalidOperationException("Restricted rollback exports require Linux.");
        var path = ExportPath(plan);
        var json = ExportJson(plan);
        if (File.Exists(path))
        {
            ValidateExportFile(path);
            if (await File.ReadAllTextAsync(path, ct) != json)
                throw new InvalidOperationException("Immutable rollback impact export mismatch.");
            return path;
        }
        await using var file = new FileStream(
            path,
            new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite,
            }
        );
        await file.WriteAsync(Encoding.UTF8.GetBytes(json), ct);
        return path;
    }

    private static void ValidateExportFile(string path)
    {
        if (
            !OperatingSystem.IsLinux()
            || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0
            || (
                File.GetUnixFileMode(path)
                & (
                    UnixFileMode.GroupRead
                    | UnixFileMode.GroupWrite
                    | UnixFileMode.GroupExecute
                    | UnixFileMode.OtherRead
                    | UnixFileMode.OtherWrite
                    | UnixFileMode.OtherExecute
                )
            ) != 0
        )
            throw new InvalidOperationException("Rollback impact file must remain owner-only and not linked.");
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";

    private static string CanonicalKey(string json)
    {
        var parsed = JsonNode.Parse(json)!.AsObject();
        return new JsonObject(
            parsed
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => KeyValuePair.Create(x.Key, x.Value?.DeepClone()))
        ).ToJsonString();
    }

    private static string KeyFrom(Table table, JsonObject values) =>
        CanonicalKey(
            new JsonObject(table.Keys.Select(x => KeyValuePair.Create(x, values[x]?.DeepClone()))).ToJsonString()
        );

    private NpgsqlCommand CreateCommand(string sql, params NpgsqlParameter[] parameters)
    {
        var command = new NpgsqlCommand(
            sql,
            (NpgsqlConnection)db.Database.GetDbConnection(),
            (NpgsqlTransaction?)db.Database.CurrentTransaction?.GetDbTransaction()
        )
        {
            CommandTimeout = db.Database.GetCommandTimeout() ?? DataMigrationOptions.DefaultCommandTimeoutSeconds,
        };
        command.Parameters.AddRange(parameters);
        return command;
    }

    private void ConfigureMigrationCommandTimeout()
    {
        var connectionString = configuration["DatabaseConnectionString"];
        var builder = string.IsNullOrWhiteSpace(connectionString)
            ? null
            : new NpgsqlConnectionStringBuilder(connectionString);
        var timeout =
            builder is not null
            && (builder.TryGetValue("Command Timeout", out _) || builder.TryGetValue("CommandTimeout", out _))
                ? builder.CommandTimeout
                : configuration.GetValue(
                    $"{DataMigrationOptions.SectionName}:{nameof(DataMigrationOptions.CommandTimeoutSeconds)}",
                    DataMigrationOptions.DefaultCommandTimeoutSeconds
                );
        db.Database.SetCommandTimeout(timeout);
    }

    private async Task CommandAsync(string sql, CancellationToken ct, params NpgsqlParameter[] parameters)
    {
        await using var command = CreateCommand(sql, parameters);
        await command.ExecuteNonQueryAsync(ct);
    }

    private async Task<string?> ScalarAsync(string sql, CancellationToken ct, params NpgsqlParameter[] parameters)
    {
        await using var command = CreateCommand(sql, parameters);
        return (string?)await command.ExecuteScalarAsync(ct);
    }
}
