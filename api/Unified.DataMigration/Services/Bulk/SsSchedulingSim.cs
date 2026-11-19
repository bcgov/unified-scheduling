using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;
using NpgsqlTypes;
using Unified.Audit;
using Unified.Audit.Options;
using Unified.Db;
using Unified.Db.Models;
using Unified.Db.Models.Abstract;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.DataMigration;
using Unified.Db.Models.Scheduling;
using Unified.Db.Models.Stats;
using Unified.Db.Models.UserManagement;

namespace Unified.DataMigration.Services.Bulk;

/// <summary>
/// In-memory model of the Scheduling target that the UNCHANGED Scheduling rules and
/// <see cref="SsWriteSession"/> logic run against in Bulk mode (<see cref="ISsSessionBackend"/> +
/// <see cref="ISsSchedulingStore"/>).
/// <para>
/// Target state is loaded once from one consistent snapshot. Rule code sees EF Core entities tracked by a
/// private, never-saved <see cref="UnifiedDbContext"/>, so change detection, relationship fix-up, identity
/// resolution and tracker clears behave exactly like PerRow. <see cref="SaveAsync"/> stands in for
/// <c>SaveChanges</c>: real ids from the table sequences, the audit interceptor's actor stamps, PostgreSQL
/// value normalisation and the database constraints PerRow relies on (not-null, varchar length, unique
/// indexes, foreign keys, named check constraints) are applied to the saved image and journaled, so savepoint
/// rollbacks revert them. Accepted saved state is persisted per chunk by <see cref="FlushAsync"/> with
/// set-based SQL in the chunk transaction.
/// </para>
/// Paths Bulk cannot reproduce exactly throw <see cref="SsBulkRefusal"/> instead of guessing.
/// </summary>
internal sealed class SsSchedulingSim : ISsSessionBackend, ISsSchedulingStore, IDisposable
{
    /// <summary>Row version of a row written in the not-yet-flushed chunk (replaced by the chunk xid).</summary>
    internal const string ChunkVersion = "\u0001chunk\u0002";

    private static readonly long PostgresEpochTicks = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;

    // Written tables in foreign-key insert order. DataMigrationRecords is managed generically too.
    private static readonly Type[] WrittenTypes =
    [
        typeof(CourtRoom),
        typeof(AssignmentDefinition),
        typeof(EventSeries),
        typeof(Event),
        typeof(AssignmentSeries),
        typeof(ShiftEntry),
        typeof(ShiftEntryUser),
        typeof(AssignmentEntry),
        typeof(ShiftAssignmentEntry),
        typeof(ShiftAssignmentEntryUser),
        typeof(DataMigrationRecord),
    ];

    private static readonly Dictionary<string, (string Sql, Func<object, bool> Ok)> KnownChecks = new(
        StringComparer.Ordinal
    )
    {
        ["CK_AssignmentDefinitions_DefaultCapacityAtLeastOne"] = (
            "\"DefaultCapacity\" >= 1",
            x => ((AssignmentDefinition)x).DefaultCapacity >= 1
        ),
        ["CK_AssignmentDefinitions_DefaultEndAfterStart"] = (
            "\"DefaultStartTime\" IS NULL OR \"DefaultEndTime\" IS NULL OR \"DefaultEndTime\" > \"DefaultStartTime\"",
            x =>
                ((AssignmentDefinition)x).DefaultStartTime is not { } start
                || ((AssignmentDefinition)x).DefaultEndTime is not { } end
                || end > start
        ),
        ["CK_AssignmentDefinitions_ExpiryAfterEffective"] = (
            "\"ExpiryDateUtc\" IS NULL OR \"ExpiryDateUtc\" > \"EffectiveDateUtc\"",
            x =>
                ((AssignmentDefinition)x).ExpiryDateUtc is not { } expiry
                || expiry > ((AssignmentDefinition)x).EffectiveDateUtc
        ),
        ["CK_AssignmentEntries_CapacityNonNegative"] = ("\"Capacity\" >= 0", x => ((AssignmentEntry)x).Capacity >= 0),
        ["CK_AssignmentSeries_CapacityNonNegative"] = ("\"Capacity\" >= 0", x => ((AssignmentSeries)x).Capacity >= 0),
        ["CK_Events_EndAfterStart"] = (
            "\"EndAtUtc\" IS NULL OR \"EndAtUtc\" > \"StartAtUtc\"",
            x => ((Event)x).EndAtUtc is not { } end || end > ((Event)x).StartAtUtc
        ),
        ["CK_EventSeries_EndAfterStart"] = (
            "\"EndAtUtc\" IS NULL OR \"EndAtUtc\" > \"StartAtUtc\"",
            x => ((EventSeries)x).EndAtUtc is not { } end || end > ((EventSeries)x).StartAtUtc
        ),
    };

    private sealed record Column(IProperty Property, string Name, NpgsqlDbType DbType);

    private sealed class Index(Func<object, object?> keyOf)
    {
        private readonly Dictionary<object, HashSet<object>> map = [];

        public void Add(object row, object id)
        {
            if (keyOf(row) is not { } key)
                return;
            if (!map.TryGetValue(key, out var ids))
                map[key] = ids = [];
            ids.Add(id);
        }

        public void Remove(object row, object id)
        {
            if (keyOf(row) is { } key && map.TryGetValue(key, out var ids))
            {
                ids.Remove(id);
                if (ids.Count == 0)
                    map.Remove(key);
            }
        }

        public IReadOnlyCollection<object> Get(object key) => map.TryGetValue(key, out var ids) ? ids : [];

        public object? KeyOf(object row) => keyOf(row);
    }

    private sealed class Table
    {
        public required IEntityType Meta { get; init; }
        public required string Name { get; init; }
        public required IProperty Key { get; init; }
        public required IProperty[] Properties { get; init; }
        public required Column[] Columns { get; init; }
        public required bool Writable { get; init; }
        public Dictionary<object, object> Rows { get; } = [];
        public HashSet<object> Baseline { get; } = [];
        public Dictionary<object, int> Written { get; } = [];
        public List<Index> Indexes { get; } = [];
        public List<(IIndex Meta, Index Index)> Uniques { get; } = [];
        public List<Func<object, bool>> Checks { get; } = [];
        public Queue<long> Ids { get; } = new();

        public object Clone(object source)
        {
            var copy = Activator.CreateInstance(Meta.ClrType)!;
            foreach (var property in Properties)
                property.PropertyInfo!.SetValue(copy, Copy(property.PropertyInfo.GetValue(source)));
            return copy;
        }

        public object KeyOf(object row) => Key.PropertyInfo!.GetValue(row)!;

        public void Add(object id, object row)
        {
            Rows[id] = row;
            foreach (var index in Indexes)
                index.Add(row, id);
            foreach (var unique in Uniques)
                unique.Index.Add(row, id);
        }

        public void Remove(object id)
        {
            if (!Rows.Remove(id, out var row))
                return;
            foreach (var index in Indexes)
                index.Remove(row, id);
            foreach (var unique in Uniques)
                unique.Index.Remove(row, id);
        }

        private static object? Copy(object? value) => value is string[] array ? array.ToArray() : value;
    }

    private readonly UnifiedDbContext db;
    private readonly UnifiedDbContext ctx;
    private readonly DataMigrationRun run;
    private readonly Guid? actor;
    private readonly AuditRecordOptions auditOptions;
    private readonly Dictionary<Type, Table> tables = [];
    private readonly Dictionary<IKey, HashSet<object>> principalKeys = [];
    private readonly Dictionary<(Table, object), object> tracked = [];
    private readonly List<Action> journal = [];
    private readonly List<(string Name, int Journal)> savepoints = [];
    private readonly List<(Table Table, object Key)> writes = [];
    private readonly List<DataMigrationMutation> mutations = [];
    private readonly List<(Table Table, bool Added, SsBulkTargetImage? Before, SsBulkTargetImage After)> audits = [];
    private readonly Dictionary<long, DataMigrationMutation> latestMutation = [];
    private readonly HashSet<long> versionedRecords = [];
    private readonly Dictionary<Guid, Dictionary<(string, string), DataMigrationOutcome>> priorOutcomes = [];
    private readonly Dictionary<Guid, User> users = [];
    private readonly Dictionary<int, int> categoryGroups = [];
    private readonly HashSet<int> linkedAssignmentSeries = [];
    private readonly Queue<long> mutationIds = new();
    private long runSequence;
    private string? expectedDigest;

    // Unmodelled tables whose keys the simulation validates foreign keys against (table, key column).
    private readonly List<(string Table, string Column)> principalColumns = [];

    /// <summary>Digest of the modelled tables and the cached rule inputs at load or after the last flush.</summary>
    internal string Digest => expectedDigest!;

    private Table Records => tables[typeof(DataMigrationRecord)];
    private Index recordByLegacy = null!;
    private Index recordByTarget = null!;
    private Index recordByEntityTarget = null!;
    private Index recordByType = null!;
    private Index shiftUserByUser = null!;
    private Index shiftUserByShift = null!;
    private Index linkByPair = null!;
    private Index linkByEntry = null!;
    private Index linkByShift = null!;
    private Index linkUserByLink = null!;
    private Index linkUserByUser = null!;
    private Index entryByDefinition = null!;
    private Index entryBySeries = null!;
    private Index seriesByDefinition = null!;
    private Index definitionByName = null!;
    private Index roomByCode = null!;

    private SsSchedulingSim(UnifiedDbContext db, DataMigrationRun run, AuditRecordOptions auditOptions)
    {
        this.db = db;
        this.run = run;
        this.auditOptions = auditOptions;
        actor = SsBulkTargets.StampActor(db);
        ctx = new UnifiedDbContext((DbContextOptions<UnifiedDbContext>)db.GetService<IDbContextOptions>());
        ctx.AuditDisabled = true;
        ctx.ChangeTracker.AutoDetectChangesEnabled = false;
        ctx.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    public bool PerRowDiagnostics => false;

    /// <summary>Saved (not merely tracked) business rows written since the last commit, by table.</summary>
    internal int PendingWrites => writes.Count;

    public void Dispose() => ctx.Dispose();

    // ------------------------------------------------------------------ load

    /// <summary>
    /// Loads the target state the Scheduling rules read, in ONE repeatable-read snapshot together with the
    /// fence digest. The caller's connection must be open and outside a transaction.
    /// </summary>
    internal static async Task<SsSchedulingSim> LoadAsync(
        UnifiedDbContext db,
        DataMigrationRun run,
        AuditRecordOptions auditOptions,
        CancellationToken ct
    )
    {
        var sim = new SsSchedulingSim(db, run, auditOptions);
        try
        {
            sim.Configure();
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
            await SsBulkStaging.ExecuteAsync(SsBulkStaging.Connection(db), "SET TRANSACTION READ ONLY", ct);
            await sim.LoadRowsAsync(ct);
            sim.expectedDigest = await sim.DigestAsync(SsBulkStaging.Connection(db), ct);
            await tx.CommitAsync(ct);
            db.ChangeTracker.Clear();
            return sim;
        }
        catch
        {
            sim.Dispose();
            throw;
        }
    }

    private void Configure()
    {
        foreach (var type in WrittenTypes.Append(typeof(SubCategory)))
        {
            var meta = ctx.Model.FindEntityType(type) ?? throw new SsBulkRefusal($"UnmappedType:{type.Name}");
            var store = StoreObjectIdentifier.Table(meta.GetTableName()!, meta.GetSchema());
            if (meta.GetSchema() is not (null or "public"))
                throw new SsBulkRefusal($"UnsupportedSchema:{type.Name}");
            var properties = meta.GetProperties().ToArray();
            if (properties.Any(x => x.IsShadowProperty() || x.PropertyInfo is null))
                throw new SsBulkRefusal($"ShadowProperty:{type.Name}");
            var key = meta.FindPrimaryKey()!.Properties.Single();
            var columns = properties
                .Where(x => !(x.IsConcurrencyToken && x.ValueGenerated != ValueGenerated.Never))
                .Select(x => new Column(x, x.GetColumnName(store)!, DbType(x)))
                .ToArray();
            if (
                properties.Any(x =>
                    x.IsConcurrencyToken
                    && x.ValueGenerated != ValueGenerated.Never
                    && !AuditPropertyExclusion.ShouldExclude(x, auditOptions)
                )
            )
                throw new SsBulkRefusal("AuditedConcurrencyToken");
            var table = new Table
            {
                Meta = meta,
                Name = meta.GetTableName()!,
                Key = key,
                Properties = properties,
                Columns = columns,
                Writable = type != typeof(SubCategory),
            };
            // Check constraints and index facets live only in the design-time model.
            var design = ctx.GetService<IDesignTimeModel>().Model.FindEntityType(type)!;
            foreach (var check in design.GetCheckConstraints())
            {
                if (!KnownChecks.TryGetValue(check.Name!, out var known) || known.Sql != check.Sql)
                    throw new SsBulkRefusal($"UnknownCheckConstraint:{check.Name}");
                table.Checks.Add(known.Ok);
            }
            foreach (var designIndex in design.GetIndexes().Where(x => x.IsUnique))
            {
                if (
                    designIndex.GetFilter() is not null
                    || designIndex.FindAnnotation("Npgsql:NullsDistinct")?.Value is false
                )
                    throw new SsBulkRefusal($"UnsupportedUniqueIndex:{designIndex.GetDatabaseName()}");
                var index = meta.GetIndexes()
                    .Single(x =>
                        x.Properties.Select(p => p.Name).SequenceEqual(designIndex.Properties.Select(p => p.Name))
                        && x.IsUnique
                    );
                var props = index.Properties.ToArray();
                table.Uniques.Add(
                    (
                        index,
                        new Index(row =>
                        {
                            var values = props.Select(p => p.PropertyInfo!.GetValue(row)).ToArray();
                            return values.Any(v => v is null)
                                ? null
                                : string.Join("\u001f", values.Select(v => Format(v!)));
                        })
                    )
                );
            }
            tables[type] = table;
        }
        static Index Add(Table table, Func<object, object?> key)
        {
            var index = new Index(key);
            table.Indexes.Add(index);
            return index;
        }
        recordByLegacy = Add(
            Records,
            x => x is DataMigrationRecord { Source: "SS" } r ? (r.EntityType, r.LegacyId) : null
        );
        recordByTarget = Add(
            Records,
            x => x is DataMigrationRecord { Source: "SS" } r ? (r.TargetType, r.TargetKey) : null
        );
        recordByEntityTarget = Add(
            Records,
            x => x is DataMigrationRecord { Source: "SS" } r ? (r.EntityType, r.TargetKey) : null
        );
        recordByType = Add(Records, x => x is DataMigrationRecord { Source: "SS" } r ? r.EntityType : null);
        shiftUserByUser = Add(tables[typeof(ShiftEntryUser)], x => ((ShiftEntryUser)x).UserId);
        shiftUserByShift = Add(tables[typeof(ShiftEntryUser)], x => ((ShiftEntryUser)x).ShiftEntryId);
        linkByPair = Add(
            tables[typeof(ShiftAssignmentEntry)],
            x => (((ShiftAssignmentEntry)x).ShiftEntryId, ((ShiftAssignmentEntry)x).AssignmentEntryId)
        );
        linkByEntry = Add(tables[typeof(ShiftAssignmentEntry)], x => ((ShiftAssignmentEntry)x).AssignmentEntryId);
        linkByShift = Add(tables[typeof(ShiftAssignmentEntry)], x => ((ShiftAssignmentEntry)x).ShiftEntryId);
        linkUserByLink = Add(
            tables[typeof(ShiftAssignmentEntryUser)],
            x => ((ShiftAssignmentEntryUser)x).ShiftAssignmentEntryId
        );
        linkUserByUser = Add(tables[typeof(ShiftAssignmentEntryUser)], x => ((ShiftAssignmentEntryUser)x).UserId);
        entryByDefinition = Add(tables[typeof(AssignmentEntry)], x => ((AssignmentEntry)x).AssignmentDefinitionId);
        entryBySeries = Add(tables[typeof(AssignmentEntry)], x => ((AssignmentEntry)x).AssignmentSeriesId);
        seriesByDefinition = Add(tables[typeof(AssignmentSeries)], x => ((AssignmentSeries)x).AssignmentDefinitionId);
        definitionByName = Add(
            tables[typeof(AssignmentDefinition)],
            x => (((AssignmentDefinition)x).LocationId, ((AssignmentDefinition)x).NormalizedName)
        );
        roomByCode = Add(
            tables[typeof(CourtRoom)],
            x => (((CourtRoom)x).Room, ((CourtRoom)x).LocationId ?? (object)DBNull.Value)
        );
    }

    private static NpgsqlDbType DbType(IProperty property) =>
        property.GetColumnType() switch
        {
            "integer" => NpgsqlDbType.Integer,
            "bigint" => NpgsqlDbType.Bigint,
            "smallint" => NpgsqlDbType.Smallint,
            "text" => NpgsqlDbType.Text,
            "boolean" => NpgsqlDbType.Boolean,
            "uuid" => NpgsqlDbType.Uuid,
            "timestamp with time zone" => NpgsqlDbType.TimestampTz,
            "time without time zone" => NpgsqlDbType.Time,
            "date" => NpgsqlDbType.Date,
            "text[]" => NpgsqlDbType.Array | NpgsqlDbType.Text,
            "xid" => NpgsqlDbType.Xid,
            var type when type.StartsWith("character varying", StringComparison.Ordinal) => NpgsqlDbType.Varchar,
            var type => throw new SsBulkRefusal($"UnsupportedColumnType:{property.DeclaringType.ClrType.Name}.{type}"),
        };

    private async Task LoadRowsAsync(CancellationToken ct)
    {
        void Fill<T>(IEnumerable<T> rows)
            where T : class
        {
            var table = tables[typeof(T)];
            foreach (var row in rows)
            {
                var key = table.KeyOf(row);
                table.Add(key, row);
                table.Baseline.Add(key);
            }
        }
        Fill(await db.CourtRooms.AsNoTracking().ToListAsync(ct));
        Fill(await db.AssignmentDefinitions.AsNoTracking().ToListAsync(ct));
        Fill(await db.EventSeries.AsNoTracking().ToListAsync(ct));
        Fill(await db.Events.AsNoTracking().ToListAsync(ct));
        Fill(await db.AssignmentSeries.AsNoTracking().ToListAsync(ct));
        Fill(await db.ShiftEntries.AsNoTracking().ToListAsync(ct));
        Fill(await db.ShiftEntryUsers.AsNoTracking().ToListAsync(ct));
        Fill(await db.AssignmentEntries.AsNoTracking().ToListAsync(ct));
        Fill(await db.ShiftAssignmentEntries.AsNoTracking().ToListAsync(ct));
        Fill(await db.ShiftAssignmentEntryUsers.AsNoTracking().ToListAsync(ct));
        Fill(await db.DataMigrationRecords.AsNoTracking().Where(x => x.Source == "SS").ToListAsync(ct));
        Fill(await db.SubCategories.AsNoTracking().ToListAsync(ct));
        foreach (
            var category in await db.StatCategories.AsNoTracking().Select(x => new { x.Id, x.GroupId }).ToListAsync(ct)
        )
            categoryGroups[category.Id] = category.GroupId;
        foreach (
            var user in await db
                .Users.AsNoTracking()
                .Select(x => new User
                {
                    Id = x.Id,
                    IdirName = x.IdirName,
                    FirstName = x.FirstName,
                    LastName = x.LastName,
                })
                .ToListAsync(ct)
        )
            users[user.Id] = user;
        foreach (
            var id in await db
                .ShiftAssignmentSeriesLinks.AsNoTracking()
                .Select(x => x.AssignmentSeriesId)
                .Distinct()
                .ToListAsync(ct)
        )
            linkedAssignmentSeries.Add(id);
        foreach (
            var mutation in await db
                .DataMigrationMutations.FromSqlRaw(
                    """
                    SELECT DISTINCT ON (m."DataMigrationRecordId") m.*
                    FROM "DataMigrationMutations" m
                    JOIN "DataMigrationRecords" r ON r."Id" = m."DataMigrationRecordId" AND r."Source" = 'SS'
                    ORDER BY m."DataMigrationRecordId", m."Id" DESC
                    """
                )
                .AsNoTracking()
                .ToListAsync(ct)
        )
            latestMutation[mutation.DataMigrationRecordId] = mutation;
        foreach (
            var id in await db
                .DataMigrationMutations.AsNoTracking()
                .Where(x => x.Version != null && x.DataMigrationRecord!.Source == "SS")
                .Select(x => x.DataMigrationRecordId)
                .Distinct()
                .ToListAsync(ct)
        )
            versionedRecords.Add(id);
        runSequence =
            await db
                .DataMigrationMutations.AsNoTracking()
                .Where(x => x.RunId == run.Id)
                .Select(x => (long?)x.Sequence)
                .MaxAsync(ct)
            ?? 0;
        // Foreign keys to tables the simulation does not model: existing principal keys.
        var connection = SsBulkStaging.Connection(db);
        foreach (
            var principal in tables
                .Values.Where(x => x.Writable)
                .SelectMany(x => x.Meta.GetForeignKeys())
                .Select(x => x.PrincipalKey)
                .Distinct()
        )
        {
            if (principal.Properties.Count != 1)
                throw new SsBulkRefusal($"CompositeForeignKey:{principal.DeclaringEntityType.ClrType.Name}");
            if (tables.ContainsKey(principal.DeclaringEntityType.ClrType))
            {
                if (!principal.IsPrimaryKey())
                    throw new SsBulkRefusal($"AlternateKeyReference:{principal.DeclaringEntityType.ClrType.Name}");
                continue;
            }
            var owner = principal.DeclaringEntityType;
            var column = principal
                .Properties[0]
                .GetColumnName(StoreObjectIdentifier.Table(owner.GetTableName()!, owner.GetSchema()));
            var keys = new HashSet<object>();
            foreach (
                var value in await SsBulkStaging.QueryAsync(
                    connection,
                    $"SELECT \"{column}\" FROM \"{owner.GetTableName()}\"",
                    reader => reader.GetValue(0),
                    ct
                )
            )
                keys.Add(value);
            principalKeys[principal] = keys;
            // Migration ledger tables are fenced by the SS advisory lock and written by the run itself.
            if (!owner.GetTableName()!.StartsWith("DataMigration", StringComparison.Ordinal))
                principalColumns.Add((owner.GetTableName()!, column!));
        }
    }

    private static readonly string[] FencedTables =
    [
        "CourtRooms",
        "AssignmentDefinitions",
        "EventSeries",
        "Events",
        "AssignmentSeries",
        "ShiftEntries",
        "ShiftEntryUsers",
        "AssignmentEntries",
        "ShiftAssignmentEntries",
        "ShiftAssignmentEntryUsers",
        "DataMigrationRecords",
        "SubCategories",
    ];

    // Cached rule inputs outside the modelled tables, digested by content (the columns the rules read), so an
    // unrelated update (for example a login timestamp on a user) does not refuse the run.
    private static readonly (string Table, string Sql)[] ReadSetDigests =
    [
        (
            "Users",
            "SELECT count(*)::bigint, coalesce(sum(hashtext(concat_ws('|', \"Id\", \"IdirName\", \"FirstName\", \"LastName\"))::bigint), 0)::bigint FROM \"Users\""
        ),
        (
            "StatCategories",
            "SELECT count(*)::bigint, coalesce(sum(hashtext(concat_ws('|', \"Id\", \"GroupId\"))::bigint), 0)::bigint FROM \"StatCategories\""
        ),
        (
            "ShiftAssignmentSeriesLinks",
            "SELECT count(DISTINCT \"AssignmentSeriesId\")::bigint, coalesce(sum(DISTINCT hashtext(\"AssignmentSeriesId\"::text)::bigint), 0)::bigint FROM \"ShiftAssignmentSeriesLinks\""
        ),
    ];

    /// <summary>Tables locked (SHARE) per chunk so the cached rule inputs cannot change under a flush.</summary>
    private IEnumerable<string> ReadSetTables =>
        ReadSetDigests
            .Select(x => x.Table)
            .Concat(principalColumns.Select(x => x.Table))
            .Append("SubCategories")
            .Distinct(StringComparer.Ordinal);

    /// <summary>
    /// Row count and row-version digest of every table the simulation models, plus a content digest of every
    /// other target input the rules read from the snapshot (users, stat category groups, linked assignment
    /// series, principal keys of unmodelled foreign keys).
    /// </summary>
    private async Task<string> DigestAsync(NpgsqlConnection connection, CancellationToken ct)
    {
        var parts = FencedTables
            .Select(x =>
                $"SELECT count(*)::bigint, coalesce(sum(hashtext(xmin::text || ctid::text)::bigint), 0)::bigint FROM \"{x}\""
            )
            .Concat(ReadSetDigests.Select(x => x.Sql))
            .Concat(
                principalColumns
                    .Distinct()
                    .Select(x =>
                        $"SELECT count(*)::bigint, coalesce(sum(hashtext(\"{x.Column}\"::text)::bigint), 0)::bigint FROM \"{x.Table}\""
                    )
            )
            .Select((sql, i) => $"SELECT {i}, * FROM ({sql}) d{i}");
        var rows = await SsBulkStaging.QueryAsync(
            connection,
            string.Join(" UNION ALL ", parts),
            reader => $"{reader.GetInt32(0)}:{reader.GetInt64(1)}:{reader.GetInt64(2)}",
            ct
        );
        return string.Join(";", rows.Order(StringComparer.Ordinal));
    }

    // ------------------------------------------------------------------ tracker

    private Table TableOf(Type type) =>
        tables.TryGetValue(type, out var table) ? table : throw new SsBulkRefusal($"UnmodelledType:{type.Name}");

    /// <summary>The tracked instance for a saved row (EF identity resolution), attaching a copy when untracked.</summary>
    private T Track<T>(Table table, object key)
        where T : class
    {
        if (tracked.TryGetValue((table, key), out var existing))
            return (T)existing;
        var instance = table.Clone(table.Rows[key]);
        ctx.Attach(instance);
        tracked[(table, key)] = instance;
        return (T)instance;
    }

    private List<T> TrackAll<T>(Table table, IEnumerable<object> keys)
        where T : class => keys.OrderBy(x => x).Select(x => Track<T>(table, x)).ToList();

    private IEnumerable<T> Saved<T>(Index index, object key)
        where T : class
    {
        var table = tables[typeof(T)];
        return index.Get(key).Select(x => (T)table.Rows[x]);
    }

    private void Reload(EntityEntry entry, Table table, object key)
    {
        var saved = table.Rows[key];
        foreach (var property in table.Properties)
        {
            var value = property.PropertyInfo!.GetValue(saved);
            var member = entry.Property(property.Name);
            member.CurrentValue = value is string[] array ? array.ToArray() : value;
            member.OriginalValue = value is string[] original ? original.ToArray() : value;
        }
        entry.State = EntityState.Unchanged;
    }

    public void EnsureTransaction() { }

    public Task CreateSavepointAsync(string name, CancellationToken ct)
    {
        savepoints.Add((name, journal.Count));
        return Task.CompletedTask;
    }

    public Task RollbackSavepointAsync(string name)
    {
        var at = savepoints.FindLastIndex(x => x.Name == name);
        if (at < 0)
            throw new InvalidOperationException($"No savepoint {name}.");
        var mark = savepoints[at].Journal;
        for (var i = journal.Count - 1; i >= mark; i--)
            journal[i]();
        journal.RemoveRange(mark, journal.Count - mark);
        savepoints.RemoveRange(at, savepoints.Count - at);
        return Task.CompletedTask;
    }

    public Task ReleaseSavepointAsync(string name, CancellationToken ct)
    {
        var at = savepoints.FindLastIndex(x => x.Name == name);
        if (at < 0)
            throw new InvalidOperationException($"No savepoint {name}.");
        savepoints.RemoveRange(at, savepoints.Count - at);
        return Task.CompletedTask;
    }

    public void ClearTracker()
    {
        ctx.ChangeTracker.Clear();
        tracked.Clear();
    }

    public Task LockGraphChildrenAsync(string targetType, string targetKey, CancellationToken ct) => Task.CompletedTask;

    public void BeforeOwnedUpdate(string path) => throw new SsBulkRefusal(path);

    // ------------------------------------------------------------------ save

    private async Task<object> NextIdAsync(Table table, CancellationToken ct)
    {
        if (table.Ids.Count == 0)
            foreach (var id in await SsBulkStaging.AllocateIdsAsync(SsBulkStaging.Connection(db), table.Name, 1000, ct))
                table.Ids.Enqueue(id);
        var next = table.Ids.Dequeue();
        // Box explicitly: a ternary would widen the int branch back to long.
        return table.Key.ClrType == typeof(int) ? (object)checked((int)next) : (object)next;
    }

    private async Task<long> NextMutationIdAsync(CancellationToken ct)
    {
        if (mutationIds.Count == 0)
            foreach (
                var id in await SsBulkStaging.AllocateIdsAsync(
                    SsBulkStaging.Connection(db),
                    "DataMigrationMutations",
                    1000,
                    ct
                )
            )
                mutationIds.Enqueue(id);
        return mutationIds.Dequeue();
    }

    public async Task SaveAsync(CancellationToken ct)
    {
        ctx.ChangeTracker.DetectChanges();
        var entries = ctx
            .ChangeTracker.Entries()
            .Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();
        if (entries.Count == 0)
            return;
        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Deleted)
                throw new SsBulkRefusal($"Delete:{entry.Metadata.ClrType.Name}");
            if (entry.Entity is DataMigrationMutation)
            {
                if (entry.State != EntityState.Added)
                    throw new SsBulkRefusal("MutationUpdate");
                continue;
            }
            if (!tables.TryGetValue(entry.Metadata.ClrType, out var table) || !table.Writable)
                throw new SsBulkRefusal($"UnmodelledWrite:{entry.Metadata.ClrType.Name}");
        }
        // Interceptor stamps (AuditableEntityInterceptor) before keys, exactly as SaveChanges.
        if (actor is { } stamp)
            foreach (var entry in entries.Where(x => x.Entity is Unified.Db.Models.Abstract.BaseEntity))
            {
                var entity = (Unified.Db.Models.Abstract.BaseEntity)entry.Entity;
                if (entry.State == EntityState.Added)
                    entity.CreatedById ??= stamp;
                else
                {
                    entry.Property(nameof(entity.UpdatedById)).CurrentValue = stamp;
                    var updatedOn = entry.Property(nameof(entity.UpdatedOn));
                    if (
                        !updatedOn.IsModified
                        || updatedOn.CurrentValue is null
                        || Equals(updatedOn.CurrentValue, updatedOn.OriginalValue)
                    )
                        updatedOn.CurrentValue = DateTimeOffset.UtcNow;
                }
            }
        ctx.ChangeTracker.DetectChanges();
        // Real ids from the table sequences (consumed even when the save later fails, like PerRow).
        foreach (var entry in entries.Where(x => x.State == EntityState.Added))
        {
            if (entry.Entity is DataMigrationMutation)
            {
                var id = entry.Property(nameof(DataMigrationMutation.Id));
                if (id.IsTemporary || Equals(id.CurrentValue, 0L))
                {
                    id.CurrentValue = await NextMutationIdAsync(ct);
                    id.IsTemporary = false;
                }
                continue;
            }
            var table = tables[entry.Metadata.ClrType];
            var key = entry.Property(table.Key.Name);
            if (key.IsTemporary || Equals(key.CurrentValue, 0) || Equals(key.CurrentValue, 0L))
            {
                key.CurrentValue = await NextIdAsync(table, ct);
                key.IsTemporary = false;
            }
        }
        // Dependent foreign keys from navigations (EF key propagation).
        foreach (var entry in entries)
        foreach (var navigation in entry.Navigations.OfType<ReferenceEntry>())
        {
            if (navigation.Metadata is not INavigation { IsOnDependent: true } nav)
                continue;
            if (navigation.TargetEntry is not { } principal)
                continue;
            for (var i = 0; i < nav.ForeignKey.Properties.Count; i++)
            {
                var fk = entry.Property(nav.ForeignKey.Properties[i].Name);
                var value = principal.Property(nav.ForeignKey.PrincipalKey.Properties[i].Name).CurrentValue;
                if (!Equals(fk.CurrentValue, value))
                    fk.CurrentValue = value;
            }
        }
        ctx.ChangeTracker.DetectChanges();
        entries = ctx
            .ChangeTracker.Entries()
            .Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();
        foreach (var entry in entries)
        foreach (var property in entry.Properties)
            if (property.IsTemporary)
                throw new InvalidOperationException(
                    $"Unresolved temporary value {entry.Metadata.ClrType.Name}.{property.Metadata.Name}."
                );

        // Build normalised images and validate the batch before applying anything.
        var images = new List<(EntityEntry Entry, Table Table, object Key, object Image, object? Before)>();
        var newMutations = new List<DataMigrationMutation>();
        foreach (var entry in entries)
        {
            if (entry.Entity is DataMigrationMutation mutation)
            {
                var copy = new DataMigrationMutation
                {
                    Id = mutation.Id,
                    RunId = mutation.RunId,
                    DataMigrationRecordId = mutation.DataMigrationRecordId,
                    Sequence = mutation.Sequence,
                    Operation = (string)
                        Normalize(entry.Property(nameof(mutation.Operation)).Metadata, mutation.Operation)!,
                    BeforeImage = (string?)Normalize(
                        entry.Property(nameof(mutation.BeforeImage)).Metadata,
                        mutation.BeforeImage
                    ),
                    AfterHash = (string)
                        Normalize(entry.Property(nameof(mutation.AfterHash)).Metadata, mutation.AfterHash)!,
                    Version = (string?)Normalize(entry.Property(nameof(mutation.Version)).Metadata, mutation.Version),
                };
                newMutations.Add(copy);
                continue;
            }
            var table = tables[entry.Metadata.ClrType];
            var image = Activator.CreateInstance(table.Meta.ClrType)!;
            foreach (var property in table.Properties)
            {
                var value = entry.Property(property.Name).CurrentValue;
                if (property.IsConcurrencyToken && property.ValueGenerated != ValueGenerated.Never)
                    value =
                        entry.State == EntityState.Added
                            ? 0u
                            : property.PropertyInfo!.GetValue(
                                table.Rows.GetValueOrDefault(entry.Property(table.Key.Name).CurrentValue!)
                                    ?? entry.Entity
                            );
                property.PropertyInfo!.SetValue(image, Normalize(property, value));
            }
            var key = table.KeyOf(image);
            var before = table.Rows.GetValueOrDefault(key);
            if (entry.State == EntityState.Added && before is not null)
                throw new Microsoft.EntityFrameworkCore.DbUpdateException($"Duplicate key {table.Name}.{key}.");
            if (entry.State == EntityState.Modified && before is null)
                throw new DbUpdateConcurrencyException($"Missing row {table.Name}.{key}.");
            images.Add((entry, table, key, image, before));
        }
        Validate(images, newMutations);

        // Audit images exactly as Audit.NET sees the change set (pre-save original/current values).
        var auditEvents = images
            .Where(x => x.Table.Meta.ClrType != typeof(DataMigrationRecord))
            .Select(x =>
                (
                    x.Table,
                    Added: x.Entry.State == EntityState.Added,
                    Before: x.Entry.State == EntityState.Added ? null : AuditImage(x.Table, x.Key, x.Entry, false),
                    After: AuditImage(x.Table, x.Key, x.Entry, true)
                )
            )
            .ToList();

        // Apply (journaled so an enclosing savepoint rollback reverts it).
        foreach (var (_, table, key, image, before) in images)
        {
            table.Remove(key);
            table.Add(key, image);
            table.Written[key] = table.Written.GetValueOrDefault(key) + 1;
            writes.Add((table, key));
            journal.Add(() =>
            {
                table.Remove(key);
                if (before is not null)
                    table.Add(key, before);
                if (--table.Written[key] == 0)
                    table.Written.Remove(key);
                writes.RemoveAt(writes.Count - 1);
            });
        }
        foreach (var audit in auditEvents)
        {
            audits.Add(audit);
            journal.Add(() => audits.RemoveAt(audits.Count - 1));
        }
        foreach (var mutation in newMutations)
        {
            var priorLatest = latestMutation.GetValueOrDefault(mutation.DataMigrationRecordId);
            var newlyVersioned = mutation.Version is not null && versionedRecords.Add(mutation.DataMigrationRecordId);
            var priorSequence = runSequence;
            mutations.Add(mutation);
            if (priorLatest is null || priorLatest.Id < mutation.Id)
                latestMutation[mutation.DataMigrationRecordId] = mutation;
            if (mutation.RunId == run.Id)
                runSequence = Math.Max(runSequence, mutation.Sequence);
            journal.Add(() =>
            {
                mutations.RemoveAt(mutations.Count - 1);
                if (priorLatest is null)
                    latestMutation.Remove(mutation.DataMigrationRecordId);
                else
                    latestMutation[mutation.DataMigrationRecordId] = priorLatest;
                if (newlyVersioned)
                    versionedRecords.Remove(mutation.DataMigrationRecordId);
                runSequence = priorSequence;
            });
        }
        ctx.ChangeTracker.AcceptAllChanges();
        foreach (var (entry, table, key, _, _) in images)
            tracked[(table, key)] = entry.Entity;
    }

    private SsBulkTargetImage AuditImage(Table table, object key, EntityEntry entry, bool current)
    {
        var audit = new Dictionary<string, object?>(StringComparer.Ordinal);
        var persisted = new Dictionary<string, object?>(StringComparer.Ordinal);
        var store = StoreObjectIdentifier.Table(table.Name, table.Meta.GetSchema());
        foreach (var property in table.Properties)
        {
            var member = entry.Property(property.Name);
            var value = current ? member.CurrentValue : member.OriginalValue;
            var column = property.GetColumnName(store) ?? property.Name;
            if (!property.IsConcurrencyToken)
                persisted[column] = value;
            if (!AuditPropertyExclusion.ShouldExclude(property, auditOptions))
                audit[column] = value;
        }
        return new(Convert.ToString(key, CultureInfo.InvariantCulture)!, "", "", audit, persisted);
    }

    private static object? Normalize(IProperty property, object? value)
    {
        if (value is null)
        {
            if (!property.IsNullable)
                throw new Microsoft.EntityFrameworkCore.DbUpdateException(
                    $"Null value in {property.DeclaringType.ClrType.Name}.{property.Name}."
                );
            return null;
        }
        if (property.GetTypeMapping().Converter is { } converter)
            throw new SsBulkRefusal($"ConvertedProperty:{property.DeclaringType.ClrType.Name}.{property.Name}");
        switch (value)
        {
            case string text:
                return Text(property, text);
            case string[] array:
                return array.Select(x => x is null ? null : Text(property, x, false)).ToArray();
            case DateTimeOffset date:
                if (date == DateTimeOffset.MinValue || date == DateTimeOffset.MaxValue)
                    return date;
                if (date.Offset != TimeSpan.Zero)
                    throw new SsBulkRefusal("NonUtcOffset");
                var relative = date.UtcTicks - PostgresEpochTicks;
                return new DateTimeOffset(PostgresEpochTicks + relative - relative % 10, TimeSpan.Zero);
            case TimeOnly time:
                return new TimeOnly(time.Ticks - time.Ticks % 10);
            case DateTime:
            case double:
            case float:
            case decimal:
                throw new SsBulkRefusal($"UnmodelledValue:{property.DeclaringType.ClrType.Name}.{property.Name}");
            default:
                return value;
        }
    }

    private static string Text(IProperty property, string text, bool limit = true)
    {
        if (text.Contains('\0', StringComparison.Ordinal))
            throw new Microsoft.EntityFrameworkCore.DbUpdateException("Invalid byte sequence (NUL) in text.");
        if (!limit || property.GetMaxLength() is not { } max || text.Length <= max)
            return text;
        // varchar(n) counts characters; an over-length value whose excess is only spaces is truncated.
        var count = 0;
        var index = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (count == max)
                break;
            count++;
            index += rune.Utf16SequenceLength;
        }
        if (count < max || index == text.Length)
            return text;
        if (text.AsSpan(index).ContainsAnyExcept(' '))
            throw new Microsoft.EntityFrameworkCore.DbUpdateException(
                $"Value too long for {property.DeclaringType.ClrType.Name}.{property.Name}."
            );
        return text[..index];
    }

    private static string Format(object value) =>
        value switch
        {
            DateTimeOffset date => date.UtcTicks.ToString(CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture)!,
        };

    private void Validate(
        List<(EntityEntry Entry, Table Table, object Key, object Image, object? Before)> images,
        List<DataMigrationMutation> newMutations
    )
    {
        var pending = images
            .GroupBy(x => x.Table)
            .ToDictionary(x => x.Key, x => x.ToDictionary(y => y.Key, y => y.Image));
        foreach (var (_, table, key, image, _) in images)
        {
            foreach (var check in table.Checks)
                if (!check(image))
                    throw new Microsoft.EntityFrameworkCore.DbUpdateException($"Check constraint on {table.Name}.");
            foreach (var (meta, index) in table.Uniques)
            {
                if (index.KeyOf(image) is not { } value)
                    continue;
                foreach (var owner in index.Get(value))
                {
                    if (Equals(owner, key))
                        continue;
                    // The other row is changed in this same save: only its new value counts.
                    if (pending[table].TryGetValue(owner, out var replaced) && !Equals(index.KeyOf(replaced), value))
                        continue;
                    throw new Microsoft.EntityFrameworkCore.DbUpdateException(
                        $"Unique violation {meta.GetDatabaseName()}."
                    );
                }
                if (pending[table].Any(x => !Equals(x.Key, key) && Equals(index.KeyOf(x.Value), value)))
                    throw new Microsoft.EntityFrameworkCore.DbUpdateException(
                        $"Unique violation {meta.GetDatabaseName()}."
                    );
            }
            foreach (var fk in table.Meta.GetForeignKeys())
            {
                var values = fk.Properties.Select(p => p.PropertyInfo!.GetValue(image)).ToArray();
                if (values.Any(x => x is null))
                    continue;
                if (!PrincipalExists(fk.PrincipalKey, values.Single()!, pending))
                    throw new Microsoft.EntityFrameworkCore.DbUpdateException(
                        $"Foreign key violation {table.Name}.{fk.Properties[0].Name}."
                    );
            }
        }
        foreach (var mutation in newMutations)
            if (
                !Records.Rows.ContainsKey(mutation.DataMigrationRecordId)
                    && pending.GetValueOrDefault(Records)?.ContainsKey(mutation.DataMigrationRecordId) != true
                || mutation.RunId != run.Id
            )
                throw new Microsoft.EntityFrameworkCore.DbUpdateException("Foreign key violation mutation record.");
    }

    private bool PrincipalExists(IKey principal, object value, Dictionary<Table, Dictionary<object, object>> pending)
    {
        if (tables.TryGetValue(principal.DeclaringEntityType.ClrType, out var table))
            return table.Rows.ContainsKey(value) || pending.GetValueOrDefault(table)?.ContainsKey(value) == true;
        if (principalKeys.TryGetValue(principal, out var keys))
            return keys.Contains(value);
        throw new SsBulkRefusal($"UnmodelledPrincipal:{principal.DeclaringEntityType.ClrType.Name}");
    }

    // ------------------------------------------------------------------ session backend reads

    private IEnumerable<DataMigrationRecord> SavedRecords(Index index, object key) =>
        index.Get(key).Select(x => (DataMigrationRecord)Records.Rows[x]);

    private DataMigrationRecord? SavedRecord(string type, string legacyId) =>
        SavedRecords(recordByLegacy, (type, legacyId)).SingleOrDefault();

    public Task<DataMigrationRecord?> RecordAsync(string type, string legacyId, CancellationToken ct) =>
        Task.FromResult(
            SavedRecord(type, legacyId) is { } record ? Track<DataMigrationRecord>(Records, record.Id) : null
        );

    public Task<DataMigrationRecord> RequiredRecordAsync(string type, string legacyId, CancellationToken ct) =>
        Task.FromResult(
            Track<DataMigrationRecord>(Records, SavedRecords(recordByLegacy, (type, legacyId)).Single().Id)
        );

    public Task<string?> TargetKeyAsync(string type, string legacyId, CancellationToken ct) =>
        Task.FromResult(SavedRecord(type, legacyId)?.TargetKey);

    public Task<List<DataMigrationRecord>> RecordsAsync(
        string type,
        IReadOnlyCollection<string> legacyIds,
        CancellationToken ct
    ) =>
        Task.FromResult(
            TrackAll<DataMigrationRecord>(
                Records,
                legacyIds
                    .Distinct()
                    .Select(x => SavedRecord(type, x))
                    .Where(x => x is not null)
                    .Select(x => (object)x!.Id)
            )
        );

    public Task<List<DataMigrationRecord>> AliasesAsync(
        string targetType,
        string targetKey,
        long? exceptId,
        CancellationToken ct
    ) =>
        Task.FromResult(
            TrackAll<DataMigrationRecord>(
                Records,
                recordByTarget.Get((targetType, targetKey)).Where(x => exceptId is null || (long)x != exceptId)
            )
        );

    public Task<bool> GroupHasOtherMemberAsync(
        string type,
        string targetKey,
        string[] legacyIds,
        CancellationToken ct
    ) =>
        Task.FromResult(
            SavedRecords(recordByEntityTarget, (type, targetKey)).Any(x => !legacyIds.Contains(x.LegacyId))
        );

    public void AddRecord(DataMigrationRecord record) => ctx.Add(record);

    public void AddMutation(DataMigrationMutation mutation) => ctx.Add(mutation);

    public Task<long> NextSequenceAsync(Guid runId, CancellationToken ct)
    {
        if (runId != run.Id)
            throw new SsBulkRefusal("ForeignRunSequence");
        return Task.FromResult(runSequence + 1);
    }

    public Task<DataMigrationMutation?> LatestMutationAsync(long recordId, CancellationToken ct) =>
        Task.FromResult(
            latestMutation.TryGetValue(recordId, out var mutation)
                ? new DataMigrationMutation
                {
                    Id = mutation.Id,
                    RunId = mutation.RunId,
                    DataMigrationRecordId = mutation.DataMigrationRecordId,
                    Sequence = mutation.Sequence,
                    Operation = mutation.Operation,
                    BeforeImage = mutation.BeforeImage,
                    AfterHash = mutation.AfterHash,
                    Version = mutation.Version,
                }
                : null
        );

    public Task<bool> HasVersionedMutationAsync(long recordId, CancellationToken ct) =>
        Task.FromResult(versionedRecords.Contains(recordId));

    public Task<bool> UserExistsAsync(Guid id, CancellationToken ct) => Task.FromResult(users.ContainsKey(id));

    private (Table Table, object Key) Target(string targetType, object targetId)
    {
        var metadata =
            ctx.Model.GetEntityTypes().SingleOrDefault(x => x.ClrType.Name == targetType)
            ?? throw new SsReject("UnknownTargetType");
        if (!tables.TryGetValue(metadata.ClrType, out var table) || table.Meta.ClrType == typeof(DataMigrationRecord))
            throw new SsBulkRefusal($"UnmodelledTarget:{targetType}");
        var pk = table.Key;
        object key =
            pk.ClrType == typeof(Guid)
                ? Guid.Parse(Convert.ToString(targetId, CultureInfo.InvariantCulture)!)
                : Convert.ChangeType(targetId, pk.ClrType, CultureInfo.InvariantCulture);
        return (table, key);
    }

    private string Version(Table table, object key) =>
        table.Written.ContainsKey(key)
            ? ChunkVersion
            : ((Unified.Db.Models.Abstract.BaseEntity)table.Rows[key]).ConcurrencyToken.ToString(
                CultureInfo.InvariantCulture
            );

    private IEnumerable<(ReferenceEntry Reference, Table Table, object? Key)> EventReferences(EntityEntry entry) =>
        entry
            .References.Where(x => x.Metadata.Name is "Event" or "EventSeries")
            .Select(x =>
            {
                var nav = (INavigation)x.Metadata;
                var fk = entry.Property(nav.ForeignKey.Properties.Single().Name).CurrentValue;
                return (x, tables[nav.TargetEntityType.ClrType], fk);
            });

    public Task<string> TargetVersionAsync(string targetType, object targetId, bool locked, CancellationToken ct)
    {
        var (table, key) = Target(targetType, targetId);
        if (!table.Rows.ContainsKey(key))
            throw new SsReject("MissingCrosswalkTarget");
        var version = Version(table, key);
        var target = Track<object>(table, key);
        var entry = ctx.Entry(target);
        if (locked)
            Reload(entry, table, key);
        foreach (var (reference, eventTable, eventKey) in EventReferences(entry).ToList())
        {
            if (eventKey is not null && eventTable.Rows.ContainsKey(eventKey))
                Track<object>(eventTable, eventKey);
            if (reference.CurrentValue is { } targetEvent)
            {
                var id = ctx.Entry(targetEvent).Property("Id").CurrentValue!;
                if (!eventTable.Rows.ContainsKey(id))
                    throw new InvalidOperationException("Sequence contains no elements");
                version += ":" + Version(eventTable, id);
                if (locked)
                    Reload(ctx.Entry(targetEvent), eventTable, id);
            }
        }
        return Task.FromResult(version);
    }

    public Task<string> TargetFingerprintAsync(string targetType, object targetId, CancellationToken ct)
    {
        var (table, key) = Target(targetType, targetId);
        if (!table.Rows.ContainsKey(key) && !tracked.ContainsKey((table, key)))
            throw new SsReject("MissingCrosswalkTarget");
        var target = tracked.TryGetValue((table, key), out var existing) ? existing : Track<object>(table, key);
        var entry = ctx.Entry(target);
        var fields = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in entry.Properties.Where(x => SsWriteSession.TargetFields.Contains(x.Metadata.Name)))
            fields[property.Metadata.Name] = property.CurrentValue;
        foreach (var collection in entry.Collections.Where(x => x.Metadata.Name == "Users"))
        {
            var childTable = tables[((INavigation)collection.Metadata).TargetEntityType.ClrType];
            var index = childTable.Meta.ClrType == typeof(ShiftEntryUser) ? shiftUserByShift : linkUserByLink;
            foreach (var child in index.Get(key).ToList())
                Track<object>(childTable, child);
            fields["Users"] = collection
                .CurrentValue!.Cast<object>()
                .Select(x =>
                    Convert.ToString(ctx.Entry(x).Property("UserId").CurrentValue, CultureInfo.InvariantCulture)
                )
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();
        }
        if (target is AssignmentEntry assignmentEntry)
        {
            var links = Saved<ShiftAssignmentEntry>(linkByEntry, assignmentEntry.Id)
                .OrderBy(x => x.ShiftEntryId)
                .ToList();
            fields["AssignedShiftUsers"] = links
                .Select(x => new
                {
                    x.ShiftEntryId,
                    Users = Saved<ShiftAssignmentEntryUser>(linkUserByLink, x.Id)
                        .Select(y => y.UserId)
                        .OrderBy(y => y)
                        .ToArray(),
                })
                .ToArray();
        }
        foreach (var (reference, eventTable, eventKey) in EventReferences(entry).ToList())
        {
            if (eventKey is not null && eventTable.Rows.ContainsKey(eventKey))
                Track<object>(eventTable, eventKey);
            if (reference.TargetEntry is { } eventEntry)
                foreach (
                    var property in eventEntry.Properties.Where(x =>
                        SsWriteSession.TargetFields.Contains(x.Metadata.Name)
                    )
                )
                    fields[$"{reference.Metadata.Name}.{property.Metadata.Name}"] = property.CurrentValue;
        }
        return Task.FromResult(SsWriteSession.Hash(new { targetType, fields }));
    }

    // ------------------------------------------------------------------ Scheduling store

    public Task<List<string>> RetainedMemberIdsAsync(CancellationToken ct) =>
        Task.FromResult(SavedRecords(recordByType, "SchedulingMember").Select(x => x.LegacyId).ToList());

    public Task<bool> ClassificationExistsAsync(int subcategory, int category, CancellationToken ct) =>
        Task.FromResult(
            tables[typeof(SubCategory)].Rows.TryGetValue(subcategory, out var row)
                && ((SubCategory)row).CategoryId == category
                && categoryGroups.TryGetValue(category, out var group)
                && group != 3
        );

    public Task<List<CourtRoom>> RoomsAsync(string code, int? location, CancellationToken ct) =>
        Task.FromResult(
            TrackAll<CourtRoom>(tables[typeof(CourtRoom)], roomByCode.Get((code, location ?? (object)DBNull.Value)))
        );

    public async Task AddRoomAsync(CourtRoom room, CancellationToken ct)
    {
        ctx.Add(room);
        await SaveAsync(ct);
    }

    public Task<bool> DefinitionNameTakenAsync(
        int location,
        string normalized,
        int[] exceptIds,
        CancellationToken ct
    ) => Task.FromResult(definitionByName.Get((location, normalized)).Any(x => !exceptIds.Contains((int)x)));

    public async Task AddDefinitionAsync(AssignmentDefinition definition, CancellationToken ct)
    {
        ctx.Add(definition);
        await SaveAsync(ct);
    }

    public Task<AssignmentDefinition> DefinitionAsync(int id, CancellationToken ct) =>
        Task.FromResult(
            tables[typeof(AssignmentDefinition)].Rows.ContainsKey(id)
                ? Track<AssignmentDefinition>(tables[typeof(AssignmentDefinition)], id)
                : throw new InvalidOperationException("Sequence contains no elements")
        );

    public Task<User> UserAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(
            users.TryGetValue(id, out var user)
                ? new User
                {
                    Id = user.Id,
                    IdirName = user.IdirName,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                }
                : throw new InvalidOperationException("Sequence contains no elements")
        );

    private Event? SavedEvent(int id) => tables[typeof(Event)].Rows.TryGetValue(id, out var row) ? (Event)row : null;

    private IEnumerable<ShiftEntry> SavedShiftsOf(Guid user)
    {
        var shifts = tables[typeof(ShiftEntry)];
        return Saved<ShiftEntryUser>(shiftUserByUser, user)
            .Select(x => x.ShiftEntryId)
            .Distinct()
            .Where(x => shifts.Rows.ContainsKey(x))
            .Select(x => (ShiftEntry)shifts.Rows[x]);
    }

    public Task<bool> ShiftOnDayExistsAsync(
        int location,
        Guid user,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct
    ) =>
        Task.FromResult(
            SavedShiftsOf(user)
                .Any(x =>
                    SavedEvent(x.EventId) is { } e
                    && e.LocationId == location
                    && e.StatusTypeCode != "cancelled"
                    && e.StartAtUtc >= from
                    && e.StartAtUtc < to
                )
        );

    public async Task AddShiftAsync(ShiftEntry shift, CancellationToken ct)
    {
        ctx.Add(shift);
        await SaveAsync(ct);
    }

    public Task<List<(string LegacyId, string TargetKey)>> ResolvedShiftsAsync(string[] legacyIds, CancellationToken ct)
    {
        // Database ORDER BY uses the column collation; numeric legacy ids sort identically ordinally.
        if (legacyIds.Any(x => x.Length == 0 || !x.All(char.IsAsciiDigit)))
            throw new SsBulkRefusal("NonNumericShiftLegacyId");
        return Task.FromResult(
            legacyIds
                .Distinct()
                .Select(x => SavedRecord("Shift", x))
                .Where(x => x is not null)
                .Select(x => (x!.LegacyId, x.TargetKey))
                .OrderBy(x => x.LegacyId, StringComparer.Ordinal)
                .ToList()
        );
    }

    public async Task AddAssignmentEntryAsync(AssignmentEntry entry, CancellationToken ct)
    {
        ctx.Add(entry);
        await SaveAsync(ct);
    }

    public Task<List<ShiftEntry>> OverlappingShiftsAsync(
        int location,
        Guid user,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken ct
    ) =>
        Task.FromResult(
            TrackAll<ShiftEntry>(
                tables[typeof(ShiftEntry)],
                SavedShiftsOf(user)
                    .Where(x =>
                        SavedEvent(x.EventId) is { } e
                        && e.LocationId == location
                        && e.StatusTypeCode != "cancelled"
                        && e.StartAtUtc < end
                        && e.EndAtUtc > start
                    )
                    .Select(x => (object)x.Id)
            )
        );

    public Task<bool> FutureAssignmentConflictAsync(
        Guid user,
        int entryId,
        int definitionId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct
    )
    {
        var links = tables[typeof(ShiftAssignmentEntry)];
        var entries = tables[typeof(AssignmentEntry)];
        return Task.FromResult(
            Saved<ShiftAssignmentEntryUser>(linkUserByUser, user)
                .Select(x => links.Rows.GetValueOrDefault(x.ShiftAssignmentEntryId) as ShiftAssignmentEntry)
                .Any(link =>
                    link is not null
                    && link.AssignmentEntryId != entryId
                    && entries.Rows.GetValueOrDefault(link.AssignmentEntryId) is AssignmentEntry entry
                    && entry.AssignmentDefinitionId == definitionId
                    && SavedEvent(entry.EventId) is { } e
                    && e.StatusTypeCode != "cancelled"
                    && e.StartAtUtc >= from
                    && e.StartAtUtc < to
                )
        );
    }

    public Task<ShiftAssignmentEntry?> ShiftAssignmentAsync(int shiftId, int entryId, CancellationToken ct)
    {
        var table = tables[typeof(ShiftAssignmentEntry)];
        var id = linkByPair.Get((shiftId, entryId)).SingleOrDefault();
        if (id is null)
            return Task.FromResult<ShiftAssignmentEntry?>(null);
        var link = Track<ShiftAssignmentEntry>(table, id);
        foreach (var user in linkUserByLink.Get(id).ToList())
            Track<ShiftAssignmentEntryUser>(tables[typeof(ShiftAssignmentEntryUser)], user);
        return Task.FromResult<ShiftAssignmentEntry?>(link);
    }

    public void AddShiftAssignment(ShiftAssignmentEntry link) => ctx.Add(link);

    public void AddShiftAssignmentUser(ShiftAssignmentEntry link, ShiftAssignmentEntryUser user) =>
        link.Users.Add(user);

    public Task<bool> AnyContainingShiftAsync(int[] ids, DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        var shifts = tables[typeof(ShiftEntry)];
        return Task.FromResult(
            ids.Distinct()
                .Any(id =>
                    shifts.Rows.TryGetValue(id, out var row)
                    && SavedEvent(((ShiftEntry)row).EventId) is { } e
                    && e.StartAtUtc <= start
                    && e.EndAtUtc >= end
                )
        );
    }

    public Task<bool> RecordExistsAsync(string type, string legacyId, CancellationToken ct) =>
        Task.FromResult(SavedRecord(type, legacyId) is not null);

    public async Task<DataMigrationOutcome?> PriorOutcomeAsync(
        Guid? runId,
        string type,
        string legacyId,
        CancellationToken ct
    )
    {
        if (runId is not { } id)
            return null;
        // PerRow sees only the current run's already-committed batches; Bulk commits differ.
        if (id == run.Id)
            throw new SsBulkRefusal("PriorOutcomeOfCurrentRun");
        if (!priorOutcomes.TryGetValue(id, out var outcomes))
        {
            outcomes = (
                await db.DataMigrationOutcomes.AsNoTracking().Where(x => x.RunId == id).ToListAsync(ct)
            ).ToDictionary(x => (x.EntityType, x.LegacyId));
            priorOutcomes[id] = outcomes;
        }
        return outcomes.GetValueOrDefault((type, legacyId));
    }

    public Task<AssignmentEntry> AssignmentEntryWithEventAsync(int id, CancellationToken ct)
    {
        var table = tables[typeof(AssignmentEntry)];
        if (!table.Rows.TryGetValue(id, out var row))
            throw new InvalidOperationException("Sequence contains no elements");
        var entry = Track<AssignmentEntry>(table, id);
        var eventId = ((AssignmentEntry)row).EventId;
        if (tables[typeof(Event)].Rows.ContainsKey(eventId))
            Track<Event>(tables[typeof(Event)], eventId);
        return Task.FromResult(entry);
    }

    public async Task AddSeriesAsync(
        AssignmentSeries series,
        IReadOnlyList<AssignmentEntry> members,
        CancellationToken ct
    )
    {
        ctx.Add(series);
        await SaveAsync(ct);
    }

    public Task<List<DataMigrationRecord>> SchedulingMembersAsync(CancellationToken ct) =>
        Task.FromResult(TrackAll<DataMigrationRecord>(Records, recordByType.Get("SchedulingMember")));

    public Task<List<DataMigrationRecord>> RootRecordsAsync(IReadOnlyCollection<string> types, CancellationToken ct) =>
        Task.FromResult(
            types
                .Distinct()
                .SelectMany(x => recordByType.Get(x))
                .Select(x => (DataMigrationRecord)Records.Clone(Records.Rows[x]))
                .ToList()
        );

    public Task<List<DataMigrationRecord>> RecordsByIdAsync(long[] ids, CancellationToken ct) =>
        Task.FromResult(
            TrackAll<DataMigrationRecord>(
                Records,
                ids.Distinct()
                    .Where(x => Records.Rows.TryGetValue(x, out var row) && ((DataMigrationRecord)row).Source == "SS")
                    .Select(x => (object)x)
            )
        );

    public void Detach() => ClearTracker();

    public async Task FlushAndDetachAsync(CancellationToken ct)
    {
        await SaveAsync(ct);
        ClearTracker();
    }

    public Task<bool> HasUnownedDependentsAsync(
        int[] definitions,
        int[] shifts,
        int[] entries,
        int[] series,
        CancellationToken ct
    )
    {
        var result =
            definitions.Any(d => entryByDefinition.Get(d).Any(x => !entries.Contains((int)x)))
            || series.Any(s => entryBySeries.Get(s).Any(x => !entries.Contains((int)x)))
            || definitions.Any(d => seriesByDefinition.Get(d).Any(x => !series.Contains((int)x)))
            || shifts
                .SelectMany(x => linkByShift.Get(x))
                .Select(x => (ShiftAssignmentEntry)tables[typeof(ShiftAssignmentEntry)].Rows[x])
                .Any(x => !entries.Contains(x.AssignmentEntryId))
            || entries
                .SelectMany(x => linkByEntry.Get(x))
                .Select(x => (ShiftAssignmentEntry)tables[typeof(ShiftAssignmentEntry)].Rows[x])
                .Any(x => !shifts.Contains(x.ShiftEntryId))
            || series.Any(linkedAssignmentSeries.Contains);
        return Task.FromResult(result);
    }

    // ------------------------------------------------------------------ chunk persistence

    /// <summary>Upper bound for one chunk's lock acquisition before the chunk fails (the run stays resumable).</summary>
    internal static TimeSpan LockWaitLimit { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Takes the chunk's table locks without ever becoming a deadlock participant that PostgreSQL resolves by
    /// aborting someone else. Read-only rule inputs are locked first (SHARE), then the written tables (SHARE
    /// ROW EXCLUSIVE): the same order a concurrent Training chunk or an application transaction uses (users before
    /// crosswalks / scheduling rows). Each attempt is bounded well below <c>deadlock_timeout</c> and then rolls
    /// back to a savepoint, releasing every lock it took, so in any wait cycle this run backs off first.
    /// </summary>
    private async Task LockAsync(NpgsqlConnection connection, CancellationToken ct)
    {
        var sql =
            "LOCK TABLE "
            + string.Join(", ", ReadSetTables.Select(x => $"\"{x}\""))
            + " IN SHARE MODE; LOCK TABLE "
            + string.Join(", ", FencedTables.Where(x => x != "SubCategories").Select(x => $"\"{x}\""))
            + ", \"DataMigrationMutations\" IN SHARE ROW EXCLUSIVE MODE";
        var deadlockMs = (
            await SsBulkStaging.QueryAsync(
                connection,
                "SELECT (extract(epoch FROM current_setting('deadlock_timeout')::interval) * 1000)::int",
                reader => reader.GetInt32(0),
                ct
            )
        ).Single();
        // Two statements per attempt, each bounded by a quarter of deadlock_timeout.
        var attemptMs = Math.Max(10, deadlockMs / 4);
        var deadline = DateTimeOffset.UtcNow + LockWaitLimit;
        for (var attempt = 0; ; attempt++)
        {
            await SsBulkStaging.ExecuteAsync(
                connection,
                $"SAVEPOINT ss_bulk_lock; SET LOCAL statement_timeout = {attemptMs}",
                ct
            );
            try
            {
                await SsBulkStaging.ExecuteAsync(connection, sql, ct);
                await SsBulkStaging.ExecuteAsync(
                    connection,
                    "SET LOCAL statement_timeout = DEFAULT; RELEASE SAVEPOINT ss_bulk_lock",
                    ct
                );
                return;
            }
            catch (PostgresException error)
                when (error.SqlState is PostgresErrorCodes.QueryCanceled or PostgresErrorCodes.LockNotAvailable
                    && !ct.IsCancellationRequested
                    && DateTimeOffset.UtcNow < deadline
                )
            {
                // Rolling back to the savepoint releases the locks of this attempt and the timeout setting.
                await SsBulkStaging.ExecuteAsync(
                    connection,
                    "ROLLBACK TO SAVEPOINT ss_bulk_lock; RELEASE SAVEPOINT ss_bulk_lock",
                    ct
                );
                await Task.Delay(
                    TimeSpan.FromMilliseconds(Random.Shared.Next(20, 100 + 20 * Math.Min(attempt, 20))),
                    ct
                );
            }
        }
    }

    /// <summary>
    /// Persists every row saved since the previous flush, inside the open chunk transaction, and returns
    /// the chunk xid. Refuses (throws) when the modelled target changed outside this run since the
    /// snapshot / previous chunk.
    /// </summary>
    internal async Task<uint> FlushAsync(NpgsqlConnection connection, SsBulkAudit audit, CancellationToken ct)
    {
        if (savepoints.Count > 0)
            throw new InvalidOperationException("Bulk chunk flush inside an open unit.");
        await LockAsync(connection, ct);
        if (await DigestAsync(connection, ct) != expectedDigest)
            throw new SsBulkRefusal("TargetChangedDuringBulkRun");
        var xid = (
            await SsBulkStaging.QueryAsync(
                connection,
                "SELECT (txid_current() % 4294967296)::bigint",
                reader => reader.GetInt64(0),
                ct
            )
        ).Single();
        var version = ((uint)xid).ToString(CultureInfo.InvariantCulture);
        var dirty = writes
            .Distinct()
            .GroupBy(x => x.Table)
            .ToDictionary(x => x.Key, x => x.Select(y => y.Key).ToList());
        foreach (var type in WrittenTypes)
        {
            var table = tables[type];
            if (!dirty.TryGetValue(table, out var keys))
                continue;
            var inserts = keys.Where(x => !table.Baseline.Contains(x)).Order().Select(x => table.Rows[x]).ToList();
            if (inserts.Count > 0)
                await CopyAsync(connection, table, inserts, ct);
        }
        foreach (var type in WrittenTypes)
        {
            var table = tables[type];
            if (!dirty.TryGetValue(table, out var keys))
                continue;
            var updates = keys.Where(table.Baseline.Contains).Order().Select(x => table.Rows[x]).ToList();
            if (updates.Count > 0)
                await UpdateAsync(connection, table, updates, ct);
        }
        foreach (var mutation in mutations)
            if (mutation.Version?.Contains(ChunkVersion, StringComparison.Ordinal) == true)
                mutation.Version = mutation.Version.Replace(ChunkVersion, version, StringComparison.Ordinal);
        if (mutations.Count > 0)
            await CopyMutationsAsync(connection, ct);
        foreach (
            var group in audits.GroupBy(x => x.Table).OrderBy(x => Array.IndexOf(WrittenTypes, x.Key.Meta.ClrType))
        )
        foreach (var item in group)
        {
            if (item.Added)
                audit.Added(item.Table.Meta.ClrType.Name, item.Table.Name, [item.After]);
            else
                audit.Modified(item.Table.Meta.ClrType.Name, item.Table.Name, item.Before!, item.After);
        }
        await audit.FlushAsync(ct);
        expectedDigest = await DigestAsync(connection, ct);
        return (uint)xid;
    }

    /// <summary>Marks the flushed rows as committed (after the chunk transaction committed).</summary>
    internal void Committed(uint xid)
    {
        foreach (var (table, key) in writes.Distinct())
        {
            table.Baseline.Add(key);
            if (table.Rows[key] is BaseEntity entity)
                entity.ConcurrencyToken = xid;
            table.Written.Remove(key);
        }
        writes.Clear();
        journal.Clear();
        audits.Clear();
        mutations.Clear();
    }

    private static object? ProviderValue(Column column, object row) => column.Property.PropertyInfo!.GetValue(row);

    private static async Task CopyAsync(
        NpgsqlConnection connection,
        Table table,
        List<object> rows,
        CancellationToken ct
    )
    {
        await using var importer = await SsBulkStaging.ImportAsync(
            connection,
            $"COPY \"{table.Name}\" ({string.Join(", ", table.Columns.Select(x => $"\"{x.Name}\""))}) FROM STDIN (FORMAT BINARY)",
            ct
        );
        foreach (var row in rows)
        {
            await importer.StartRowAsync(ct);
            foreach (var column in table.Columns)
            {
                var value = ProviderValue(column, row);
                if (value is null)
                    await importer.WriteNullAsync(ct);
                else
                    await importer.WriteAsync(value, column.DbType, ct);
            }
        }
        await importer.CompleteAsync(ct);
    }

    private static async Task UpdateAsync(
        NpgsqlConnection connection,
        Table table,
        List<object> rows,
        CancellationToken ct
    )
    {
        var columns = table
            .Columns.Select(
                (x, i) =>
                    new SsBulkColumn<object>(
                        $"c{i}",
                        x.Property.GetColumnType(),
                        x.DbType,
                        row => ProviderValue(x, row)
                    )
            )
            .ToArray();
        await SsBulkStaging.StageAsync(connection, "ss_sched_update", columns, rows, ct);
        var key = Array.FindIndex(table.Columns, x => x.Property == table.Key);
        var set = string.Join(
            ", ",
            table.Columns.Select((x, i) => (x, i)).Where(x => x.i != key).Select(x => $"\"{x.x.Name}\" = s.c{x.i}")
        );
        var updated = await SsBulkStaging.ExecuteAsync(
            connection,
            $"UPDATE \"{table.Name}\" t SET {set} FROM ss_sched_update s WHERE t.\"{table.Columns[key].Name}\" = s.c{key}",
            ct
        );
        if (updated != rows.Count)
            throw new InvalidOperationException($"Bulk update of {table.Name} matched {updated} of {rows.Count} rows.");
    }

    private async Task CopyMutationsAsync(NpgsqlConnection connection, CancellationToken ct)
    {
        await using var importer = await SsBulkStaging.ImportAsync(
            connection,
            """
            COPY "DataMigrationMutations" ("Id","RunId","DataMigrationRecordId","Sequence","Operation","BeforeImage","AfterHash","Version")
            FROM STDIN (FORMAT BINARY)
            """,
            ct
        );
        foreach (var mutation in mutations)
        {
            if (mutation.Version is { Length: > 128 })
                throw new InvalidOperationException("Mutation version too long.");
            await importer.StartRowAsync(ct);
            await importer.WriteAsync(mutation.Id, NpgsqlDbType.Bigint, ct);
            await importer.WriteAsync(mutation.RunId, NpgsqlDbType.Uuid, ct);
            await importer.WriteAsync(mutation.DataMigrationRecordId, NpgsqlDbType.Bigint, ct);
            await importer.WriteAsync(mutation.Sequence, NpgsqlDbType.Bigint, ct);
            await importer.WriteAsync(mutation.Operation, NpgsqlDbType.Varchar, ct);
            if (mutation.BeforeImage is null)
                await importer.WriteNullAsync(ct);
            else
                await importer.WriteAsync(mutation.BeforeImage, NpgsqlDbType.Text, ct);
            await importer.WriteAsync(mutation.AfterHash, NpgsqlDbType.Varchar, ct);
            if (mutation.Version is null)
                await importer.WriteNullAsync(ct);
            else
                await importer.WriteAsync(mutation.Version, NpgsqlDbType.Varchar, ct);
        }
        await importer.CompleteAsync(ct);
    }
}
