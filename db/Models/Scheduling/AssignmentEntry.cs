using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;
using Unified.Db.Models.Calendar;

namespace Unified.Db.Models.Scheduling;

public class AssignmentEntry : BaseEntity
{
    [Key]
    public int Id { get; set; }

    public int? AssignmentSeriesId { get; set; }

    public AssignmentSeries? AssignmentSeries { get; set; }

    public int AssignmentDefinitionId { get; set; }

    public AssignmentDefinition AssignmentDefinition { get; set; } = null!;

    public int EventId { get; set; }

    public Event? Event { get; set; }

    public int Capacity { get; set; }

    public ICollection<ShiftAssignmentEntry> ShiftAssignmentEntries { get; set; } = [];
}
