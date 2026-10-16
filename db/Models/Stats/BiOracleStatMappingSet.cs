using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;

namespace Unified.Db.Models.Stats;

public class BiOracleStatMappingSet : BaseEntity
{
    [Key]
    public int Id { get; set; }

    public DateOnly EffectiveDate { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    public ICollection<BiOracleStatMapping> Mappings { get; set; } = [];

    public ICollection<BiOracleEtlRun> EtlRuns { get; set; } = [];
}
