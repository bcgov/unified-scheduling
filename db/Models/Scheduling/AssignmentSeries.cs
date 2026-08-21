using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Stats;

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

    public int CategoryId { get; set; }
    public StatCategory Category { get; set; } = null!;
    public int SubCategoryId { get; set; }
    public SubCategory SubCategory { get; set; } = null!;

    public ICollection<AssignmentEntry> AssignmentEntries { get; set; } = [];

    public ICollection<ShiftAssignmentSeriesLink> ShiftAssignmentSeriesLinks { get; set; } = [];
}
