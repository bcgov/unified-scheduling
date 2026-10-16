using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Lookup;
using Unified.Db.Models.UserManagement;

namespace Unified.Db.Models.TimeOff;

public class TimeOffSeries : BaseEntity
{
    [Key]
    public int Id { get; set; }

    public int EventSeriesId { get; set; }

    public EventSeries? EventSeries { get; set; }

    public int LeaveTypeId { get; set; }

    public LeaveType? LeaveType { get; set; }

    public ICollection<TimeOffSeriesUser> Users { get; set; } = new List<TimeOffSeriesUser>();

    public ICollection<TimeOffEntry> TimeOffEntries { get; set; } = new List<TimeOffEntry>();
}
