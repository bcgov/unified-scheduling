using System.ComponentModel.DataAnnotations;
using Unified.Db.Models.Abstract;

namespace Unified.Db.Models.Stats;

public class BiOracleEtlRun : BaseEntity
{
    [Key]
    public int Id { get; set; }

    public Guid LoadId { get; set; }

    public DateOnly ReportingMonth { get; set; }

    public int MappingSetId { get; set; }

    public BiOracleStatMappingSet? MappingSet { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    [Required]
    public string Status { get; set; } = BiOracleEtlRunStatus.Running;

    public int SourceRecordCount { get; set; }

    public int StagedCellCount { get; set; }

    public int LoadedCellCount { get; set; }

    public string? ErrorMessage { get; set; }

    public ICollection<BiOracleStatStage> StagedCells { get; set; } = [];
}

public static class BiOracleEtlRunStatus
{
    public const string Running = "Running";
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
}
