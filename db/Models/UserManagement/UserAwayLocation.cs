using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;
using Unified.Db.Models.Calendar;

namespace Unified.Db.Models.UserManagement;

/// <summary>
/// Links a <see cref="Calendar.Event"/> record to the user it applies to.
/// Away-location-specific fields (dates, timezone, location, comment, expiry) live on the
/// linked <see cref="Event"/> instead of being duplicated on this table.
/// </summary>
public class UserAwayLocation : BaseEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int EventId { get; set; }

    public virtual Event Event { get; set; } = null!;

    [Required]
    public Guid UserId { get; set; }

    public virtual User User { get; set; } = null!;
}
