using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Lookup;

namespace Unified.Db.Models.Scheduling;

public class AssignmentEntry : BaseEntity
{
    [Key]
    public int Id { get; set; }

    public int? AssignmentSeriesId { get; set; }

    public AssignmentSeries? AssignmentSeries { get; set; }

    public int EventId { get; set; }

    public Event? Event { get; set; }

    public int AssignmentCategoryTypeId { get; set; }

    public AssignmentCategoryType? AssignmentCategoryType { get; set; }

    public int AssignmentSubCategoryTypeId { get; set; }

    public AssignmentSubCategoryType? AssignmentSubCategoryType { get; set; }

    public int AssignmentTypeId { get; set; }

    public AssignmentType? AssignmentType { get; set; }

    public int Capacity { get; set; }

    public ICollection<ShiftAssignmentEntry> ShiftAssignmentEntries { get; set; } = [];
}
