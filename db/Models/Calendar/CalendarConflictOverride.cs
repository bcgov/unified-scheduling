using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;

namespace Unified.Db.Models.Calendar;

public sealed class CalendarConflictOverride : BaseEntity
{
    [Key]
    public int Id { get; set; }

    public string FirstSourceModule { get; set; } = string.Empty;

    public string FirstEventId { get; set; } = string.Empty;

    public string SecondSourceModule { get; set; } = string.Empty;

    public string SecondEventId { get; set; } = string.Empty;

    public Guid ResourceId { get; set; }

    public string Note { get; set; } = string.Empty;

    public DateTimeOffset? InvalidatedOn { get; set; }
}
