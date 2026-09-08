using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Lookup;

namespace Unified.Db.Models.UserManagement;

/// <summary>
/// Links a <see cref="Event"/> record to the user and leave type it applies to.
/// Leave-specific fields (dates, timezone, comment, expiry) live on the linked
/// <see cref="Event"/> instead of being duplicated on this table.
/// </summary>
public class UserLeave : BaseEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int EventId { get; set; }

    public virtual Event Event { get; set; } = null!;

    [Required]
    public Guid UserId { get; set; }

    public virtual User User { get; set; } = null!;

    [Required]
    public int LeaveTypeId { get; set; }

    public virtual LeaveType LeaveType { get; set; } = null!;
}
