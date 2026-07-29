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

    public ICollection<ShiftEntryUser> Users { get; set; } = [];
}
