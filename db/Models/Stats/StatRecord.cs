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

    // Numeric values are assumed for all metrics (see SubCategoryMetric -> StatMetric.UnitOfMeasure).
    // Supporting non-numeric value types would require new functionality.
    public decimal Value { get; set; }

    public string? Comment { get; set; }

    [Required]
    public string Status { get; set; } = StatRecordStatus.Draft;
}

public static class StatRecordStatus
{
    public const string Draft = "Draft";
    public const string Submitted = "Submitted";
}
