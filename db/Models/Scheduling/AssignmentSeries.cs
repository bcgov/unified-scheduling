using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;
using Unified.Db.Models.Calendar;
using Unified.Db.Models.Lookup;

namespace Unified.Db.Models.Scheduling;

public class AssignmentSeries : BaseEntity
{
    [Key]
    public int Id { get; set; }

    public int EventSeriesId { get; set; }

    public EventSeries? EventSeries { get; set; }

    public int AssignmentCategoryTypeId { get; set; }

    public AssignmentCategoryType? AssignmentCategoryType { get; set; }

    public int AssignmentSubCategoryTypeId { get; set; }

    public AssignmentSubCategoryType? AssignmentSubCategoryType { get; set; }

    public int AssignmentTypeId { get; set; }

    public AssignmentType? AssignmentType { get; set; }

    public int Capacity { get; set; }

    public ICollection<AssignmentEntry> AssignmentEntries { get; set; } = [];
}
