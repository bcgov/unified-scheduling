using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;

namespace Unified.Db.Models.Calendar;

public sealed class CalendarConflictOverride : BaseEntity
{
    [Key]
    public int Id { get; set; }

    public int FirstEventId { get; set; }

    public Event FirstEvent { get; set; } = null!;

    public int SecondEventId { get; set; }

    public Event SecondEvent { get; set; } = null!;

    public Guid ResourceId { get; set; }

    public string Note { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset? InvalidatedOn { get; set; }
}
