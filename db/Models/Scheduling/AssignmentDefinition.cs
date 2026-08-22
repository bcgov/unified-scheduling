using System.ComponentModel.DataAnnotations;
using Unified.Db.Models;
using Unified.Db.Models.Abstract;
using Unified.Db.Models.Stats;

namespace Unified.Db.Models.Scheduling;

public sealed class AssignmentDefinition : BaseEntity
{
    [Key]
    public int Id { get; set; }
    public int LocationId { get; set; }
    public required string Name { get; set; }
    public required string NormalizedName { get; set; }
    public string? Description { get; set; }
    public int CategoryId { get; set; }
    public int SubCategoryId { get; set; }
    public string? Color { get; set; }
    public TimeOnly? DefaultStartTime { get; set; }
    public TimeOnly? DefaultEndTime { get; set; }
    public int DefaultCapacity { get; set; }
    public DateTimeOffset EffectiveDateUtc { get; set; }
    public DateTimeOffset? ExpiryDateUtc { get; set; }
    public Location Location { get; set; } = null!;
    public StatCategory Category { get; set; } = null!;
    public SubCategory SubCategory { get; set; } = null!;
    public ICollection<AssignmentSeries> AssignmentSeries { get; set; } = [];
    public ICollection<AssignmentEntry> AssignmentEntries { get; set; } = [];
}
