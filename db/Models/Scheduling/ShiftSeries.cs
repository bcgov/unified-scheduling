using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;
using Unified.Db.Models.Calendar;

namespace Unified.Db.Models.Scheduling;

public class ShiftSeries : BaseEntity
{
    [Key]
    public int Id { get; set; }

    public int EventSeriesId { get; set; }

    public EventSeries? EventSeries { get; set; }

    public ICollection<ShiftSeriesUser> Users { get; set; } = [];

    public ICollection<ShiftEntry> ShiftEntries { get; set; } = [];
}
