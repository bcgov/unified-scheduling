using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Unified.Db.Models;

/// <summary>
/// Append-only record of every entity change captured by AuditRecordInterceptor.
/// No FK constraints — survives deletion of the originating entity.
/// </summary>
public class AuditRecord
{
    [Key]
    public long Id { get; set; }

    /// <summary>Always UTC.</summary>
    public DateTimeOffset OccurredOn { get; set; }

    /// <summary>Null for system/background operations.</summary>
    public Guid? ActorUserId { get; set; }

    /// <summary>Display name of the actor at the time of the event.</summary>
    public string? ActorName { get; set; }

    /// <summary>Added | Modified | Deleted</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>EF Core entity type name.</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Entity Primary key value</summary>
    public string EntityPK { get; set; } = string.Empty;

    /// <summary>Actual database table name.</summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>Previous values — populated for Modified and Deleted.</summary>
    public string? OldValues { get; set; }

    /// <summary>New values — populated for Added and Modified.</summary>
    public string? NewValues { get; set; }

    /// <summary>Column names that changed — populated for Modified only.</summary>
    public string[]? ChangedColumns { get; set; }

    /// <summary>Module that originated the change (e.g. user-management).</summary>
    public string? SourceModule { get; set; }

    /// <summary>Request trace/correlation id for cross-module debugging.</summary>
    public string? CorrelationId { get; set; }
}
