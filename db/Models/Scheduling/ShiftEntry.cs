using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;
using Unified.Db.Models.Calendar;

namespace Unified.Db.Models.Scheduling;

public class ShiftEntry : BaseEntity
{
    [Key]
    public int Id { get; set; }

    public int? ShiftSeriesId { get; set; }

    public ShiftSeries? ShiftSeries { get; set; }

    public int EventId { get; set; }

    public Event? Event { get; set; }

    public int LunchAvailableMinutes { get; set; } = 0;

    public int WorkedLunchMinutes { get; set; } = 0;

    public ICollection<ShiftEntryUser> Users { get; set; } = [];

    public ICollection<ShiftAssignmentEntry> ShiftAssignmentEntries { get; set; } = [];
}
