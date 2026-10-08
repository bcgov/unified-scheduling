using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Lookup;

namespace Unified.Db.Models.TimeOff;

public class TimeOffEntry : BaseEntity
{
    [Key]
    public int Id { get; set; }

    public int? TimeOffSeriesId { get; set; }

    public TimeOffSeries? TimeOffSeries { get; set; }

    public int LeaveTypeId { get; set; }

    public LeaveType? LeaveType { get; set; }

    public int EventId { get; set; }

    public Event? Event { get; set; }

    public ICollection<TimeOffEntryUser> Users { get; set; } = new List<TimeOffEntryUser>();
}
