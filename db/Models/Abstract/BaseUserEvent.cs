using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.UserManagement;

namespace Unified.Db.Models.Abstract;

/// <summary>
/// Abstract base for user-scoped calendar events (away locations, leave, training, etc.).
/// All dates are stored as UTC <see cref="DateTimeOffset"/> values.
/// </summary>
public abstract class BaseUserEvent : BaseEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    public Guid UserId { get; set; }

    public virtual User User { get; set; } = null!;

    [Required]
    public DateTimeOffset StartAtUtc { get; set; }

    [Required]
    public DateTimeOffset EndAtUtc { get; set; }

    public DateTimeOffset? ExpiryAtUtc { get; set; }

    public string? ExpiryReason { get; set; }

    [MaxLength(500)]
    public string? Comment { get; set; }

    /// <summary>
    /// IANA timezone ID used when the event was created (e.g. "America/Vancouver").
    /// Stored for display/conversion purposes; all dates are stored as UTC.
    /// </summary>
    [MaxLength(100)]
    public string? Timezone { get; set; }
}
