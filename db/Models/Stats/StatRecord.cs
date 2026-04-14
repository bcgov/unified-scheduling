using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;

namespace Unified.Db.Models.Stats;

public class StatRecord : BaseEntity
{
    [Key]
    public int Id { get; set; }

    public DateOnly DateFrom { get; set; }

    public DateOnly DateTo { get; set; }

    [Required]
    public string PeriodType { get; set; } = string.Empty;

    public int LocationId { get; set; }

    public Location? Location { get; set; }

    public int SubCategoryMetricId { get; set; }

    public SubCategoryMetric? SubCategoryMetric { get; set; }

    public decimal Value { get; set; }

    public string? Comment { get; set; }
}
