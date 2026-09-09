using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;

namespace Unified.Db.Models.Scheduling;

public class ShiftAssignmentSeriesLink : BaseEntity
{
    [Key]
    public int Id { get; set; }

    public int ShiftSeriesId { get; set; }

    public ShiftSeries? ShiftSeries { get; set; }

    public int AssignmentSeriesId { get; set; }

    public AssignmentSeries? AssignmentSeries { get; set; }

    public ICollection<ShiftAssignmentSeriesLinkUser> Users { get; set; } = [];

    public ICollection<ShiftAssignmentEntry> EntryLinks { get; set; } = [];
}
