using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;

namespace Unified.Db.Models.Stats;

public class BiOracleStatMapping : BaseEntity
{
    [Key]
    public int Id { get; set; }

    public int MappingSetId { get; set; }

    public BiOracleStatMappingSet? MappingSet { get; set; }

    public int SubCategoryMetricId { get; set; }

    public SubCategoryMetric? SubCategoryMetric { get; set; }

    [Required]
    public string TargetTable { get; set; } = string.Empty;

    [Required]
    public string TargetColumn { get; set; } = string.Empty;
}
