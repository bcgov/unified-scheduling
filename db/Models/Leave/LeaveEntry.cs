using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Lookup;

namespace Unified.Db.Models.Leave;

public class LeaveEntry : BaseEntity
{
    [Key]
    public int Id { get; set; }

    public int? LeaveSeriesId { get; set; }

    public LeaveSeries? LeaveSeries { get; set; }

    public int LeaveTypeId { get; set; }

    public LeaveType? LeaveType { get; set; }

    public int EventId { get; set; }

    public Event? Event { get; set; }

    public ICollection<LeaveEntryUser> Users { get; set; } = new List<LeaveEntryUser>();
}
