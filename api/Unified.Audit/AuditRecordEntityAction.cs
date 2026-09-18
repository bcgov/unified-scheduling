using System.Text.Json;
using Audit.Core;
using Audit.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Unified.Audit.Options;
using Unified.Db.Models;

namespace Unified.Audit;

/// <summary>
/// Populates an <see cref="AuditRecord"/> for Audit.NET's built-in <c>EntityFrameworkDataProvider</c>
/// (wired via <c>UseEntityFramework</c> in <see cref="AuditModule.UseAuditModule"/>). Every audited
/// entity type maps to this same <see cref="AuditRecord"/> table, so property names never match
/// between the source entity and <see cref="AuditRecord"/> - <c>IgnoreMatchedProperties</c> is set
/// and every field below is populated explicitly instead.
/// </summary>
public sealed class AuditRecordEntityAction(ICurrentActorResolver actorResolver, AuditRecordOptions options)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Returns false to skip creating an AuditRecord entirely - used when the entity save failed, so
    /// a failed operation never produces an audit entry describing a change that never happened.
    /// </summary>
    public bool Populate(AuditEvent auditEvent, EventEntry entry, AuditRecord record)
    {
        if (auditEvent.GetEntityFrameworkEvent()?.Success != true)
        {
            return false;
        }

        var actor = actorResolver.Resolve();
        var entityType = entry.GetEntry().Metadata;
        var changedColumns = entry
            .Changes?.Select(change => change.ColumnName)
            .Where(columnName => !ShouldExclude(entityType, entry.Table, columnName))
            .ToArray();

        record.OccurredOn = DateTimeOffset.UtcNow;
        record.ActorUserId = actor.ActorUserId;
        record.ActorName = actor.ActorName;
        record.Action = MapAction(entry.Action);
        record.EntityType = entityType.ClrType.Name;
        record.TableName = entry.Table; // Every table in this schema uses a single "Id" column as its primary key.
        record.EntityPK = entry.PrimaryKey.Values.Single()?.ToString() ?? string.Empty;
        record.OldValues = BuildValues(entry, entityType, oldValues: true);
        record.NewValues = BuildValues(entry, entityType, oldValues: false);
        record.ChangedColumns = changedColumns is { Length: > 0 } ? changedColumns : null;
        // Populated from the ambient System.Diagnostics.Activity (Audit.Core.Configuration.IncludeActivityTrace, set in AuditModule).
        record.CorrelationId = auditEvent.Activity?.TraceId;

        return true;
    }

    // entry.Action reflects the state EF Core captured pre-save (Insert/Update/Delete); by the time
    // this runs the save has already completed and the live EntityEntry.State has been reset.
    private string? BuildValues(EventEntry entry, IEntityType entityType, bool oldValues)
    {
        Dictionary<string, object?> values = [];

        if (string.Equals(entry.Action, "Update", StringComparison.Ordinal))
        {
            foreach (var change in entry.Changes ?? [])
            {
                if (ShouldExclude(entityType, entry.Table, change.ColumnName))
                {
                    continue;
                }

                values[change.ColumnName] = oldValues ? change.OriginalValue : change.NewValue;
            }
        }
        else
        {
            var isInsert = string.Equals(entry.Action, "Insert", StringComparison.Ordinal);
            if ((isInsert && oldValues) || (!isInsert && !oldValues))
            {
                return null;
            }

            foreach (var (columnName, value) in entry.ColumnValues)
            {
                if (ShouldExclude(entityType, entry.Table, columnName))
                {
                    continue;
                }

                values[columnName] = value;
            }
        }

        return values.Count == 0 ? null : JsonSerializer.Serialize(values, SerializerOptions);
    }

    private bool ShouldExclude(IEntityType entityType, string tableName, string columnName)
    {
        var property = entityType.FindProperty(columnName) ?? FindByColumnName(entityType, tableName, columnName);
        return property is not null && AuditPropertyExclusion.ShouldExclude(property, options);
    }

    // Audit.NET reports the flattened database column name, which can differ from the CLR property
    // name (e.g. shadow FKs, [Column]-mapped properties) - fall back to matching against the real table.
    private static IProperty? FindByColumnName(IEntityType entityType, string tableName, string columnName)
    {
        var storeObject = StoreObjectIdentifier.Table(tableName, entityType.GetSchema());
        return entityType
            .GetProperties()
            .FirstOrDefault(property =>
                string.Equals(property.GetColumnName(storeObject), columnName, StringComparison.Ordinal)
            );
    }

    private static string MapAction(string action) =>
        action switch
        {
            "Insert" => "Added",
            "Update" => "Modified",
            "Delete" => "Deleted",
            _ => action,
        };
}
