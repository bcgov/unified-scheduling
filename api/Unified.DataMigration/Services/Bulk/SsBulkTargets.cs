using System.Globalization;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;
using Unified.Db;
using Unified.Db.Models.UserManagement;

namespace Unified.DataMigration.Services.Bulk;

/// <summary>Post-image of one target row: migration fingerprint, row version and audited column values.</summary>
internal sealed record SsBulkTargetImage(
    string Key,
    string Fingerprint,
    string Version,
    IReadOnlyDictionary<string, object?> AuditValues
);

/// <summary>
/// Set-based equivalents of the per-row <see cref="SsWriteSession"/> target fingerprint and version reads.
/// Fingerprints are computed through the same EF metadata and the same hash shape so PerRow and Bulk
/// receipts are interchangeable (a Bulk run can be replayed by PerRow and vice versa).
/// Graph targets (entities with a <c>Users</c> collection or <c>Event</c>/<c>EventSeries</c> references)
/// are deliberately unsupported here; the Scheduling bulk workstream must extend this before using it.
/// </summary>
internal static class SsBulkTargets
{
    /// <summary>Must stay identical to <c>SsWriteSession.SafeTargetFields</c> (guarded by a unit test).</summary>
    internal static readonly IReadOnlySet<string> SafeTargetFields = new HashSet<string>(StringComparer.Ordinal)
    {
        "Name",
        "NormalizedName",
        "Description",
        "Title",
        "Notes",
        "Comment",
        "ExpiryReason",
        "CancellationReason",
        "CancelledReason",
        "FirstName",
        "LastName",
        "Email",
        "IdirName",
        "IdirId",
        "KeyCloakId",
        "BadgeNumber",
        "Rank",
        "EmployeeNumber",
        "SourceModule",
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
    };

    // Default Unified.Audit exclusions (AuditRecordOptions.ExcludedPropertyNames); byte[] and
    // [AuditIgnore] properties are excluded as in AuditPropertyExclusion.
    private static readonly string[] AuditExcludedNames = ["xmin", "ConcurrencyToken", "IdirId", "KeyCloakId"];

    /// <summary>
    /// Loads <paramref name="keys"/> of <typeparamref name="TEntity"/> and returns their post-images.
    /// Uses a short-lived tracking query and clears the change tracker afterwards.
    /// </summary>
    internal static async Task<Dictionary<string, SsBulkTargetImage>> ImagesAsync<TEntity, TKey>(
        UnifiedDbContext db,
        IReadOnlyCollection<TKey> keys,
        CancellationToken ct
    )
        where TEntity : class
        where TKey : notnull
    {
        var result = new Dictionary<string, SsBulkTargetImage>(StringComparer.Ordinal);
        if (keys.Count == 0)
            return result;
        var metadata = db.Model.FindEntityType(typeof(TEntity)) ?? throw new SsReject("UnknownTargetType");
        Guard(metadata);
        var pk = metadata.FindPrimaryKey()!.Properties.Single();
        var versions = await VersionsAsync(db, metadata, keys.Cast<object>().ToArray(), ct);
        db.ChangeTracker.Clear();
        foreach (var batch in keys.Distinct().Chunk(5000))
        {
            var entities = await db.Set<TEntity>()
                .Where(x => batch.Contains(EF.Property<TKey>(x, pk.Name)))
                .ToListAsync(ct);
            foreach (var entity in entities)
            {
                var entry = db.Entry(entity);
                var key = Convert.ToString(entry.Property(pk.Name).CurrentValue, CultureInfo.InvariantCulture)!;
                var fields = new SortedDictionary<string, object?>(StringComparer.Ordinal);
                foreach (var property in entry.Properties.Where(x => SafeTargetFields.Contains(x.Metadata.Name)))
                    fields[property.Metadata.Name] = property.CurrentValue;
                var targetType = metadata.ClrType.Name;
                var audit = new Dictionary<string, object?>(StringComparer.Ordinal);
                var table = StoreObjectIdentifier.Table(metadata.GetTableName()!, metadata.GetSchema());
                foreach (var property in entry.Properties)
                    if (!AuditExcluded(property.Metadata))
                        audit[property.Metadata.GetColumnName(table) ?? property.Metadata.Name] = property.CurrentValue;
                result[key] = new(
                    key,
                    SsWriteSession.Hash(new { targetType, fields }),
                    versions.GetValueOrDefault(key) ?? throw new SsReject("MissingCrosswalkTarget"),
                    audit
                );
            }
        }
        db.ChangeTracker.Clear();
        return result;
    }

    /// <summary>Current <c>xmin</c> per key (the PerRow mutation <c>Version</c> value).</summary>
    internal static async Task<Dictionary<string, string>> VersionsAsync(
        UnifiedDbContext db,
        IEntityType metadata,
        object[] keys,
        CancellationToken ct,
        bool forUpdate = false
    )
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (keys.Length == 0)
            return result;
        var pk = metadata.FindPrimaryKey()!.Properties.Single();
        var table = $"\"{metadata.GetSchema() ?? "public"}\".\"{metadata.GetTableName()}\"";
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        var array =
            pk.ClrType == typeof(Guid)
                ? (object)
                    keys.Select(x => x is Guid g ? g : Guid.Parse(Convert.ToString(x, CultureInfo.InvariantCulture)!))
                        .ToArray()
                : keys.Select(x => Convert.ToInt64(x, CultureInfo.InvariantCulture)).ToArray();
        var cast = pk.ClrType == typeof(Guid) ? "uuid[]" : "bigint[]";
        var rows = await SsBulkStaging.QueryAsync(
            connection,
            $"SELECT \"{pk.Name}\"::text, xmin::text FROM {table} WHERE \"{pk.Name}\" = ANY(@keys::{cast})"
                + (forUpdate ? " FOR UPDATE" : ""),
            reader => (Key: reader.GetString(0), Version: reader.GetString(1)),
            ct,
            new NpgsqlParameter("keys", array)
        );
        foreach (var row in rows)
            result[row.Key] = row.Version;
        return result;
    }

    /// <summary>
    /// True when the context stamps <c>CreatedById</c>/<c>UpdatedById</c> through the Unified audit interceptor.
    /// Background (Hangfire) saves resolve to the Unified system user, so Bulk stamps the same actor.
    /// </summary>
    internal static Guid? StampActor(UnifiedDbContext db) =>
        db.GetService<IDbContextOptions>()
            .FindExtension<CoreOptionsExtension>()
            ?.Interceptors?.Any(x => x.GetType().Name == "AuditableEntityInterceptor") == true
            ? User.SystemUser
            : null;

    internal static bool AuditEnabled(UnifiedDbContext db) =>
        !global::Audit.Core.Configuration.AuditDisabled && !db.AuditDisabled;

    private static bool AuditExcluded(IProperty property) =>
        property.ClrType == typeof(byte[])
        || property.PropertyInfo?.GetCustomAttribute<global::Audit.EntityFramework.AuditIgnoreAttribute>() is not null
        || AuditExcludedNames.Any(x => string.Equals(x, property.Name, StringComparison.OrdinalIgnoreCase));

    private static void Guard(IEntityType metadata)
    {
        if (
            metadata
                .GetNavigations()
                .Cast<INavigationBase>()
                .Concat(metadata.GetSkipNavigations())
                .Any(x => x.Name is "Users" or "Event" or "EventSeries")
            || metadata.ClrType.Name == "AssignmentEntry"
        )
            throw new NotSupportedException(
                $"Bulk fingerprints do not support graph target {metadata.ClrType.Name}; extend SsBulkTargets first."
            );
    }
}
