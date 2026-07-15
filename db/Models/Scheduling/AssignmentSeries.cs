using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;
using Unified.Db.Models.Calendar;

namespace Unified.Db.Models.Scheduling;

public class AssignmentSeries : BaseEntity
{
    [Key]
    public int Id { get; set; }

    public int EventSeriesId { get; set; }

    public int AssignmentDefinitionId { get; set; }

    public AssignmentDefinition AssignmentDefinition { get; set; } = null!;

    public EventSeries? EventSeries { get; set; }

    public int Capacity { get; set; }

    public ICollection<AssignmentEntry> AssignmentEntries { get; set; } = [];

    public ICollection<ShiftAssignmentSeriesLink> ShiftAssignmentSeriesLinks { get; set; } = [];
}
