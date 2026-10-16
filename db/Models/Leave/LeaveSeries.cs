using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Lookup;
using Unified.Db.Models.UserManagement;

namespace Unified.Db.Models.Leave;

public class LeaveSeries : BaseEntity
{
    [Key]
    public int Id { get; set; }

    public int EventSeriesId { get; set; }

    public EventSeries? EventSeries { get; set; }

    public int LeaveTypeId { get; set; }

    public LeaveType? LeaveType { get; set; }

    public ICollection<LeaveSeriesUser> Users { get; set; } = new List<LeaveSeriesUser>();

    public ICollection<LeaveEntry> LeaveEntries { get; set; } = new List<LeaveEntry>();
}
