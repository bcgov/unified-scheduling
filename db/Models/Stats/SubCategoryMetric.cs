using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;

namespace Unified.Db.Models.Stats;

public class SubCategoryMetric : BaseEntity
{
    [Key]
    public int Id { get; set; }

    public int SubCategoryId { get; set; }

    public SubCategory? SubCategory { get; set; }

    public int MetricId { get; set; }

    public StatMetric? Metric { get; set; }

    public int DisplayOrder { get; set; }
}
