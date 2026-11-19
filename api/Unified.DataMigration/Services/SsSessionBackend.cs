using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Unified.Db;
using Unified.Db.Models.DataMigration;

namespace Unified.DataMigration.Services;

/// <summary>
/// Thrown by a Bulk simulation when a rule reaches a path Bulk persistence does not reproduce exactly
/// (owned update, graph replacement, database behaviour it cannot model). Never converted into a row
/// outcome: it aborts the Bulk run before any business write so the operator re-runs in PerRow mode.
/// </summary>
public sealed class SsBulkRefusal(string reason) : Exception(reason);

/// <summary>
/// Storage primitives of <see cref="SsWriteSession"/>. The outcome, counter, receipt and drift logic lives
/// once in the session; PerRow binds it to EF Core and PostgreSQL savepoints (<see cref="SsEfSessionBackend"/>),
/// Scheduling Bulk binds the SAME session logic to an in-memory model of the target.
/// </summary>
internal interface ISsSessionBackend
{
    void EnsureTransaction();
    Task CreateSavepointAsync(string name, CancellationToken ct);

    /// <summary>Rolls back to and releases the savepoint (never cancelled).</summary>
    Task RollbackSavepointAsync(string name);
    Task ReleaseSavepointAsync(string name, CancellationToken ct);
    void ClearTracker();
    Task SaveAsync(CancellationToken ct);

    /// <summary>Tracked crosswalk record (SingleOrDefault semantics).</summary>
    Task<DataMigrationRecord?> RecordAsync(string type, string legacyId, CancellationToken ct);
    Task<DataMigrationRecord> RequiredRecordAsync(string type, string legacyId, CancellationToken ct);
    Task<string?> TargetKeyAsync(string type, string legacyId, CancellationToken ct);
    Task<List<DataMigrationRecord>> RecordsAsync(
        string type,
        IReadOnlyCollection<string> legacyIds,
        CancellationToken ct
    );
    Task<List<DataMigrationRecord>> AliasesAsync(
        string targetType,
        string targetKey,
        long? exceptId,
        CancellationToken ct
    );
    Task<bool> GroupHasOtherMemberAsync(string type, string targetKey, string[] legacyIds, CancellationToken ct);
    void AddRecord(DataMigrationRecord record);
    void AddMutation(DataMigrationMutation mutation);
    Task<long> NextSequenceAsync(Guid runId, CancellationToken ct);
    Task<DataMigrationMutation?> LatestMutationAsync(long recordId, CancellationToken ct);
    Task<bool> HasVersionedMutationAsync(long recordId, CancellationToken ct);
    Task<bool> UserExistsAsync(Guid id, CancellationToken ct);
    Task<string> TargetVersionAsync(string targetType, object targetId, bool locked, CancellationToken ct);
    Task<string> TargetFingerprintAsync(string targetType, object targetId, CancellationToken ct);
    Task LockGraphChildrenAsync(string targetType, string targetKey, CancellationToken ct);

    /// <summary>Called before an owned-target update or graph replacement writes through EF directly.</summary>
    void BeforeOwnedUpdate(string path);
    bool PerRowDiagnostics { get; }
}

/// <summary>PerRow storage: EF Core change tracking, PostgreSQL savepoints and row locks (original code).</summary>
internal sealed class SsEfSessionBackend(UnifiedDbContext db, SsWriteSession session) : ISsSessionBackend
{
    private Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction Transaction =>
        db.Database.CurrentTransaction ?? throw new InvalidOperationException("Job transaction required.");

    public bool PerRowDiagnostics => true;

    public void EnsureTransaction() => _ = Transaction;

    public Task CreateSavepointAsync(string name, CancellationToken ct) => Transaction.CreateSavepointAsync(name, ct);

    public async Task RollbackSavepointAsync(string name)
    {
        var tx = Transaction;
        await tx.RollbackToSavepointAsync(name, CancellationToken.None);
        await tx.ReleaseSavepointAsync(name, CancellationToken.None);
    }

    public Task ReleaseSavepointAsync(string name, CancellationToken ct) => Transaction.ReleaseSavepointAsync(name, ct);

    public void ClearTracker() => db.ChangeTracker.Clear();

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    public Task<DataMigrationRecord?> RecordAsync(string type, string legacyId, CancellationToken ct) =>
        db.DataMigrationRecords.SingleOrDefaultAsync(
            x => x.Source == "SS" && x.EntityType == type && x.LegacyId == legacyId,
            ct
        );

    public Task<DataMigrationRecord> RequiredRecordAsync(string type, string legacyId, CancellationToken ct) =>
        db.DataMigrationRecords.SingleAsync(
            x => x.Source == "SS" && x.EntityType == type && x.LegacyId == legacyId,
            ct
        );

    public Task<string?> TargetKeyAsync(string type, string legacyId, CancellationToken ct) =>
        db
            .DataMigrationRecords.Where(x => x.Source == "SS" && x.EntityType == type && x.LegacyId == legacyId)
            .Select(x => x.TargetKey)
            .SingleOrDefaultAsync(ct);

    public Task<List<DataMigrationRecord>> RecordsAsync(
        string type,
        IReadOnlyCollection<string> legacyIds,
        CancellationToken ct
    ) =>
        db
            .DataMigrationRecords.Where(x => x.Source == "SS" && x.EntityType == type && legacyIds.Contains(x.LegacyId))
            .ToListAsync(ct);

    public Task<List<DataMigrationRecord>> AliasesAsync(
        string targetType,
        string targetKey,
        long? exceptId,
        CancellationToken ct
    ) =>
        exceptId is { } except
            ? db
                .DataMigrationRecords.Where(x =>
                    x.Source == "SS" && x.Id != except && x.TargetType == targetType && x.TargetKey == targetKey
                )
                .ToListAsync(ct)
            : db
                .DataMigrationRecords.Where(x =>
                    x.Source == "SS" && x.TargetType == targetType && x.TargetKey == targetKey
                )
                .ToListAsync(ct);

    public Task<bool> GroupHasOtherMemberAsync(
        string type,
        string targetKey,
        string[] legacyIds,
        CancellationToken ct
    ) =>
        db.DataMigrationRecords.AnyAsync(
            x =>
                x.Source == "SS" && x.EntityType == type && x.TargetKey == targetKey && !legacyIds.Contains(x.LegacyId),
            ct
        );

    public void AddRecord(DataMigrationRecord record) => db.DataMigrationRecords.Add(record);

    public void AddMutation(DataMigrationMutation mutation) => db.DataMigrationMutations.Add(mutation);

    public async Task<long> NextSequenceAsync(Guid runId, CancellationToken ct) =>
        (await db.DataMigrationMutations.Where(x => x.RunId == runId).Select(x => (long?)x.Sequence).MaxAsync(ct) ?? 0)
        + 1;

    public Task<DataMigrationMutation?> LatestMutationAsync(long recordId, CancellationToken ct) =>
        db
            .DataMigrationMutations.Where(x => x.DataMigrationRecordId == recordId)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);

    public Task<bool> HasVersionedMutationAsync(long recordId, CancellationToken ct) =>
        db.DataMigrationMutations.AnyAsync(x => x.DataMigrationRecordId == recordId && x.Version != null, ct);

    public Task<bool> UserExistsAsync(Guid id, CancellationToken ct) => db.Users.AnyAsync(x => x.Id == id, ct);

    public async Task LockGraphChildrenAsync(string targetType, string targetKey, CancellationToken ct)
    {
        if (targetType == "ShiftEntry")
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT \"Id\" FROM \"ShiftEntryUsers\" WHERE \"ShiftEntryId\"={int.Parse(targetKey)} FOR UPDATE",
                ct
            );
        if (targetType == "AssignmentEntry")
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT l.\"Id\" FROM \"ShiftAssignmentEntries\" l WHERE l.\"AssignmentEntryId\"={int.Parse(targetKey)} FOR UPDATE",
                ct
            );
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT u.\"Id\" FROM \"ShiftAssignmentEntryUsers\" u JOIN \"ShiftAssignmentEntries\" l ON l.\"Id\"=u.\"ShiftAssignmentEntryId\" WHERE l.\"AssignmentEntryId\"={int.Parse(targetKey)} FOR UPDATE OF u",
                ct
            );
        }
    }

    public void BeforeOwnedUpdate(string path) { }

    // PostgreSQL row versions additionally protect private photos without hashing/copying
    // them into migration evidence. Graph event edits are fenced too. Conservatively treats edits
    // to other owners' columns as drift rather than overwriting them.
    public async Task<string> TargetVersionAsync(string targetType, object targetId, bool locked, CancellationToken ct)
    {
        var metadata =
            db.Model.GetEntityTypes().SingleOrDefault(x => x.ClrType.Name == targetType)
            ?? throw new SsReject("UnknownTargetType");
        var pk = metadata.FindPrimaryKey()!.Properties.Single();
        var key =
            pk.ClrType == typeof(Guid)
                ? (object)Guid.Parse(Convert.ToString(targetId, CultureInfo.InvariantCulture)!)
                : Convert.ChangeType(targetId, pk.ClrType, CultureInfo.InvariantCulture);
        var table = $"\"{session.SimulationSchema ?? metadata.GetSchema() ?? "public"}\".\"{metadata.GetTableName()}\"";
        var sql =
            $"SELECT xmin::text AS \"Value\" FROM {table} WHERE \"{pk.Name}\" = @key{(locked ? " FOR UPDATE" : "")}";
        var version =
            await db.Database.SqlQueryRaw<string>(sql, new NpgsqlParameter("key", key)).SingleOrDefaultAsync(ct)
            ?? throw new SsReject("MissingCrosswalkTarget");
        var target = await db.FindAsync(metadata.ClrType, [key], ct) ?? throw new SsReject("MissingCrosswalkTarget");
        if (locked)
            await db.Entry(target).ReloadAsync(ct);
        foreach (
            var eventReference in db.Entry(target).References.Where(x => x.Metadata.Name is "Event" or "EventSeries")
        )
        {
            await eventReference.LoadAsync(ct);
            if (eventReference.CurrentValue is { } targetEvent)
            {
                var eventId = db.Entry(targetEvent).Property("Id").CurrentValue!;
                var eventTable = eventReference.Metadata.Name == "Event" ? "\"Events\"" : "\"EventSeries\"";
                var eventSql =
                    $"SELECT xmin::text AS \"Value\" FROM {eventTable} WHERE \"Id\" = @key{(locked ? " FOR UPDATE" : "")}";
                version +=
                    ":"
                    + await db
                        .Database.SqlQueryRaw<string>(eventSql, new NpgsqlParameter("key", eventId))
                        .SingleAsync(ct);
                if (locked)
                    await db.Entry(targetEvent).ReloadAsync(ct);
            }
        }
        return version;
    }

    // Canonical mapped target digest is deliberately independent of the source shape.
    // Text/SSO values are hashed in memory, never stored/emitted as audit evidence.
    // Photo bytes are excluded; their private copy is additionally fenced by row versions.
    public async Task<string> TargetFingerprintAsync(string targetType, object targetId, CancellationToken ct)
    {
        var metadata =
            db.Model.GetEntityTypes().SingleOrDefault(x => x.ClrType.Name == targetType)
            ?? throw new SsReject("UnknownTargetType");
        var pkType = metadata.FindPrimaryKey()!.Properties.Single().ClrType;
        var key =
            pkType == typeof(Guid)
                ? (object)Guid.Parse(Convert.ToString(targetId, CultureInfo.InvariantCulture)!)
                : Convert.ChangeType(targetId, pkType, CultureInfo.InvariantCulture);
        var target = await db.FindAsync(metadata.ClrType, [key], ct) ?? throw new SsReject("MissingCrosswalkTarget");
        var entry = db.Entry(target);
        var fields = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in entry.Properties.Where(x => SsWriteSession.TargetFields.Contains(x.Metadata.Name)))
            fields[property.Metadata.Name] = property.CurrentValue;
        foreach (var collection in entry.Collections.Where(x => x.Metadata.Name == "Users"))
        {
            await collection.LoadAsync(ct);
            fields["Users"] = collection
                .CurrentValue!.Cast<object>()
                .Select(x =>
                    Convert.ToString(db.Entry(x).Property("UserId").CurrentValue, CultureInfo.InvariantCulture)
                )
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();
        }
        if (target is Unified.Db.Models.Scheduling.AssignmentEntry assignmentEntry)
        {
            var links = await db
                .ShiftAssignmentEntries.AsNoTracking()
                .Include(x => x.Users)
                .Where(x => x.AssignmentEntryId == assignmentEntry.Id)
                .OrderBy(x => x.ShiftEntryId)
                .ToListAsync(ct);
            fields["AssignedShiftUsers"] = links
                .Select(x => new { x.ShiftEntryId, Users = x.Users.Select(y => y.UserId).OrderBy(y => y).ToArray() })
                .ToArray();
        }
        foreach (var eventReference in entry.References.Where(x => x.Metadata.Name is "Event" or "EventSeries"))
        {
            await eventReference.LoadAsync(ct);
            if (eventReference.TargetEntry is { } eventEntry)
                foreach (
                    var property in eventEntry.Properties.Where(x =>
                        SsWriteSession.TargetFields.Contains(x.Metadata.Name)
                    )
                )
                    fields[$"{eventReference.Metadata.Name}.{property.Metadata.Name}"] = property.CurrentValue;
        }
        return SsWriteSession.Hash(new { targetType, fields });
    }
}
