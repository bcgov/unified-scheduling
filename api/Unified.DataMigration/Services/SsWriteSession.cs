using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Unified.DataMigration.Sources;
using Unified.Db;
using Unified.Db.Models.Abstract;
using Unified.Db.Models.DataMigration;

namespace Unified.DataMigration.Services;

public sealed class SsReject(string code) : Exception(code);

/// <summary>One job transaction; failed rows use savepoints, never poison independent rows.</summary>
public sealed class SsWriteSession(UnifiedDbContext db, DataMigrationRun run)
{
    public List<DataMigrationOutcome> Outcomes { get; } = [];
    private static readonly HashSet<string> PrimaryTypes =
    [
        "Region",
        "Location",
        "Role",
        "User",
        "UserRole",
        "PositionType",
        "LeaveType",
        "SheriffActingRank",
        "SheriffAwayLocation",
        "SheriffLeave",
        "LookupSortOrder",
        "Training",
        "SheriffTraining",
        "SheriffStandardTraining",
        "CourtRoom",
        "AssignmentClassification",
        "Assignment",
        "Duty",
        "DutySlot",
        "Shift",
        "LookupCode",
    ];
    public IEnumerable<DataMigrationOutcome> SourceOutcomes => Outcomes.Where(x => PrimaryTypes.Contains(x.EntityType));
    private HashSet<string> insertedTargets = [];
    public int Inserted => insertedTargets.Count;
    public int Linked { get; private set; }
    public DataMigrationRun Run => run;
    public DateTimeOffset Now => run.StartedOn!.Value;

    public static string Hash(object safeStructuralFields) =>
        DataMigrationOrchestrator.ComputePayloadHash(JsonSerializer.Serialize(safeStructuralFields));

    public void Outcome(string type, string id, string outcome, string? reason = null) =>
        Outcomes.Add(
            new DataMigrationOutcome
            {
                RunId = run.Id,
                EntityType = type,
                LegacyId = id,
                Outcome = outcome,
                Reason = reason,
            }
        );

    public T Stamp<T>(T entity)
        where T : BaseEntity
    {
        entity.CreatedOn = Now;
        // Unified audit identity, never source CreatedBy/UpdatedBy values.
        return entity;
    }

    public async Task RowAsync(string type, SsRow source, Func<Task> write, CancellationToken ct)
    {
        var tx = db.Database.CurrentTransaction ?? throw new InvalidOperationException("Job transaction required.");
        var checkpoint = Outcomes.Count;
        var inserted = insertedTargets.ToHashSet();
        var linked = Linked;
        await tx.CreateSavepointAsync("ss_row", ct);
        try
        {
            await write();
            await db.SaveChangesAsync(ct);
            await tx.ReleaseSavepointAsync("ss_row", ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            await tx.RollbackToSavepointAsync("ss_row", CancellationToken.None);
            await tx.ReleaseSavepointAsync("ss_row", CancellationToken.None);
            db.ChangeTracker.Clear();
            Outcomes.RemoveRange(checkpoint, Outcomes.Count - checkpoint);
            insertedTargets = inserted;
            Linked = linked;
            Outcome(
                type,
                source.Id,
                ex is SsReject ? "Rejected" : "Failed",
                ex is SsReject rejection ? rejection.Message : "TargetWriteFailed"
            );
        }
    }

    public async Task<string> ResolveAsync(string type, string? id, CancellationToken ct)
    {
        if (id is null)
            throw new SsReject("MissingParent");
        if (type == "User" && id == "00000000-0000-0000-0000-000000000001")
        {
            if (await db.Users.AnyAsync(x => x.Id == new Guid(id), ct))
                return id;
            throw new SsReject("MissingUnifiedSystemUser");
        }
        return await db
                .DataMigrationRecords.Where(x => x.Source == "SS" && x.EntityType == type && x.LegacyId == id)
                .Select(x => x.TargetKey)
                .SingleOrDefaultAsync(ct)
            ?? throw new SsReject("MissingParent");
    }

    public async Task<int?> ResolveIntAsync(string type, int? id, CancellationToken ct) =>
        id is null
            ? null
            : int.Parse(
                await ResolveAsync(type, id.Value.ToString(CultureInfo.InvariantCulture), ct),
                CultureInfo.InvariantCulture
            );

    public async Task<bool> ReplayAsync(
        string type,
        SsRow source,
        string sourceHash,
        bool reference,
        CancellationToken ct,
        Func<Task>? applyOwnedUpdate = null
    )
    {
        var record = await db.DataMigrationRecords.SingleOrDefaultAsync(
            x => x.Source == "SS" && x.EntityType == type && x.LegacyId == source.Id,
            ct
        );
        if (record is null)
            return false;
        // References are add/link-only, including retired rows. Never refresh linked business rows.
        if (
            !reference
            && (
                record.PayloadHash != sourceHash
                || record.SourceUpdatedAtUtc != (source.Date("UpdatedOn") ?? source.Date("CreatedOn"))
            )
        )
        {
            if (record.CreatedByRunId is null)
                throw new SsReject("LinkedOnlyTargetCannotUpdate");
            if (record.PayloadHash == sourceHash)
                throw new SsReject("SourceChangedOutsideStructuralDigestRequiresReview");
            if (applyOwnedUpdate is null)
                throw new SsReject("OwnedUpdateRequiresReview");
            var prior = await db
                .DataMigrationMutations.Where(x => x.DataMigrationRecordId == record.Id)
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync(ct);
            if (prior?.Version is null)
                throw new SsReject("MissingOriginalTargetVersionProof");
            var version = await TargetVersionAsync(record.TargetType, record.TargetKey, true, ct);
            if (
                version != prior.Version
                || record.AfterHash != await TargetFingerprintAsync(record.TargetType, record.TargetKey, ct)
            )
                throw new SsReject("OwnedTargetDrift");
            await applyOwnedUpdate();
            await db.SaveChangesAsync(ct);
            record.PayloadHash = sourceHash;
            record.AfterHash = await TargetFingerprintAsync(record.TargetType, record.TargetKey, ct);
            record.SourceUpdatedAtUtc = source.Date("UpdatedOn") ?? source.Date("CreatedOn");
            record.LastAppliedRunId = run.Id;
            db.DataMigrationMutations.Add(
                new DataMigrationMutation
                {
                    RunId = run.Id,
                    DataMigrationRecordId = record.Id,
                    Sequence =
                        (
                            await db
                                .DataMigrationMutations.Where(x => x.RunId == run.Id)
                                .Select(x => (long?)x.Sequence)
                                .MaxAsync(ct)
                            ?? 0
                        ) + 1,
                    Operation = "Update",
                    AfterHash = record.AfterHash,
                    Version = await TargetVersionAsync(record.TargetType, record.TargetKey, false, ct),
                }
            );
            Outcome(type, source.Id, "Migrated", "OwnedMappedUpdate");
            return true;
        }
        Outcome(type, source.Id, "Migrated", "ExistingCrosswalk");
        return true;
    }

    public async Task LinkAsync(
        string type,
        SsRow source,
        string targetType,
        object targetId,
        string hash,
        bool created,
        CancellationToken ct,
        string outcome = "Migrated",
        string? reason = null
    )
    {
        var owner = type switch
        {
            "Region"
            or "Location"
            or "PositionType"
            or "LeaveType"
            or "Role"
            or "User"
            or "UserRole"
            or "SheriffActingRank"
            or "SheriffLeave"
            or "SheriffAwayLocation" => "MyTeam",
            "Training" or "SheriffTraining" => "Training",
            "CourtRoom"
            or "AssignmentClassification"
            or "Assignment"
            or "AssignmentSeries"
            or "Shift"
            or "Duty"
            or "DutySlot" => "Scheduling",
            _ => throw new SsReject("UnknownEntityOwnership"),
        };
        if (owner != run.Job)
            throw new SsReject("EntityOwnershipViolation");
        var afterHash = await TargetFingerprintAsync(targetType, targetId, ct);
        var record = new DataMigrationRecord
        {
            Source = "SS",
            EntityType = type,
            LegacyId = source.Id,
            TargetType = targetType,
            TargetKey = Convert.ToString(targetId, CultureInfo.InvariantCulture)!,
            PayloadHash = hash,
            AfterHash = afterHash,
            CreatedByRunId = created ? run.Id : null,
            SourceUpdatedAtUtc = source.Date("UpdatedOn") ?? source.Date("CreatedOn"),
            LastAppliedRunId = run.Id,
        };
        db.DataMigrationRecords.Add(record);
        await db.SaveChangesAsync(ct);
        db.DataMigrationMutations.Add(
            new DataMigrationMutation
            {
                RunId = run.Id,
                DataMigrationRecordId = record.Id,
                Sequence = await db
                    .DataMigrationMutations.Where(x => x.RunId == run.Id)
                    .Select(x => (long?)x.Sequence)
                    .MaxAsync(ct)
                    is { } sequence
                    ? sequence + 1
                    : 1,
                Operation = created ? "Insert" : "Link",
                AfterHash = afterHash,
                Version = await TargetVersionAsync(targetType, targetId, false, ct),
            }
        );
        if (created)
            insertedTargets.Add($"{targetType}:{record.TargetKey}");
        else
            Linked++;
        Outcome(type, source.Id, outcome, reason);
    }

    private static readonly HashSet<string> SafeTargetFields =
    [
        "Id",
        "JustinId",
        "AgencyId",
        "JustinLocationCode",
        "ParentLocationId",
        "RegionId",
        "Timezone",
        "ExpiryDate",
        "EffectiveDate",
        "Code",
        "DisplayOrder",
        "Order",
        "IsPaid",
        "IsEnabled",
        "Gender",
        "HomeLocationId",
        "PendingRegistration",
        "LastLogin",
        "LastPhotoUpdate",
        "UserId",
        "RoleId",
        "StartAtUtc",
        "EndAtUtc",
        "ExpiryAtUtc",
        "PositionTypeId",
        "Mandatory",
        "ValidityDays",
        "AdvanceNoticeDays",
        "Rotating",
        "TrainingCategoryId",
        "TrainingId",
        "Version",
        "AwardedOn",
        "EndingOn",
        "AllDay",
        "NoticeState",
        "LeaveTypeId",
        "EventId",
        "LocationId",
        "LoanOutLocationId",
        "CategoryId",
        "SubCategoryId",
        "EffectiveDateUtc",
        "ExpiryDateUtc",
        "DefaultCapacity",
        "DefaultStartTime",
        "DefaultEndTime",
        "LunchAvailableMinutes",
        "WorkedLunchMinutes",
        "AssignmentDefinitionId",
        "AssignmentSeriesId",
        "EventSeriesId",
        "Capacity",
        "TimeZoneId",
        "StatusTypeCode",
        "EventTypeCode",
        "CancelledAt",
        "RecurrenceRule",
    ];

    // PostgreSQL row versions protect free text and photos without hashing or copying them
    // into migration evidence. Graph event edits are fenced too. Conservatively treats edits
    // to other owners' columns as drift rather than overwriting them.
    private async Task<string> TargetVersionAsync(string targetType, object targetId, bool locked, CancellationToken ct)
    {
        var metadata =
            db.Model.GetEntityTypes().SingleOrDefault(x => x.ClrType.Name == targetType)
            ?? throw new SsReject("UnknownTargetType");
        var pk = metadata.FindPrimaryKey()!.Properties.Single();
        var key =
            pk.ClrType == typeof(Guid)
                ? (object)Guid.Parse(Convert.ToString(targetId, CultureInfo.InvariantCulture)!)
                : Convert.ChangeType(targetId, pk.ClrType, CultureInfo.InvariantCulture);
        var table = $"\"{metadata.GetSchema() ?? "public"}\".\"{metadata.GetTableName()}\"";
        var sql =
            $"SELECT xmin::text AS \"Value\" FROM {table} WHERE \"{pk.Name}\" = @key{(locked ? " FOR UPDATE" : "")}";
        var version =
            await db.Database.SqlQueryRaw<string>(sql, new NpgsqlParameter("key", key)).SingleOrDefaultAsync(ct)
            ?? throw new SsReject("MissingCrosswalkTarget");
        var target = await db.FindAsync(metadata.ClrType, [key], ct) ?? throw new SsReject("MissingCrosswalkTarget");
        if (locked)
            await db.Entry(target).ReloadAsync(ct);
        var eventReference = db.Entry(target).References.SingleOrDefault(x => x.Metadata.Name == "Event");
        if (eventReference is not null)
        {
            await eventReference.LoadAsync(ct);
            if (eventReference.CurrentValue is { } targetEvent)
            {
                var eventId = db.Entry(targetEvent).Property("Id").CurrentValue!;
                var eventSql = locked
                    ? "SELECT xmin::text AS \"Value\" FROM \"Events\" WHERE \"Id\" = @key FOR UPDATE"
                    : "SELECT xmin::text AS \"Value\" FROM \"Events\" WHERE \"Id\" = @key";
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

    // Structural target digest is deliberately independent of source IDs/field shapes.
    // No names, comments, notes, SSO identities or photos may enter a migration fingerprint.
    // Text is protected by the separate original PostgreSQL row-version proof, not this digest.
    private async Task<string> TargetFingerprintAsync(string targetType, object targetId, CancellationToken ct)
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
        foreach (var property in entry.Properties.Where(x => SafeTargetFields.Contains(x.Metadata.Name)))
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
        var eventReference = entry.References.SingleOrDefault(x => x.Metadata.Name == "Event");
        if (eventReference is not null)
        {
            await eventReference.LoadAsync(ct);
            if (eventReference.TargetEntry is { } eventEntry)
                foreach (var property in eventEntry.Properties.Where(x => SafeTargetFields.Contains(x.Metadata.Name)))
                    fields[$"Event.{property.Metadata.Name}"] = property.CurrentValue;
        }
        return Hash(new { targetType, fields });
    }

    public static string? Timezone(SsRow source, bool required)
    {
        var zone = source.Text("Timezone");
        if (zone is null && !required)
            return null;
        if (string.IsNullOrWhiteSpace(zone))
            throw new SsReject("InvalidTimezone");
        try
        {
            if (!TimeZoneInfo.FindSystemTimeZoneById(zone).HasIanaId)
                throw new SsReject("InvalidTimezone");
        }
        catch (TimeZoneNotFoundException)
        {
            throw new SsReject("InvalidTimezone");
        }
        catch (InvalidTimeZoneException)
        {
            throw new SsReject("InvalidTimezone");
        }
        return zone;
    }

    public static (DateTimeOffset Start, DateTimeOffset End) Dates(SsRow source)
    {
        var start = source.Date("StartDate") ?? throw new SsReject("MissingStart");
        var end = source.Date("EndDate") ?? throw new SsReject("MissingEnd");
        if (end < start)
            throw new SsReject("ReversedDates");
        return (start, end);
    }
}
