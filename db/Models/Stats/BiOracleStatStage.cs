using System.ComponentModel.DataAnnotations;

namespace Unified.Db.Models.Stats;

public class BiOracleStatStage
{
    [Key]
    public int Id { get; set; }

    public Guid LoadId { get; set; }

    public BiOracleEtlRun? EtlRun { get; set; }

    public DateOnly ReportingMonth { get; set; }

    public int CrtLocId { get; set; }

    public bool IsLocationLevel { get; set; }

    public bool IsSupervisor { get; set; }

    [Required]
    public string TargetTable { get; set; } = string.Empty;

    [Required]
    public string TargetColumn { get; set; } = string.Empty;

    public decimal StatValue { get; set; }

    public int SourceRowCount { get; set; }
}

public static class BiOracleTargetTable
{
    public const string SheriffServices = "SHERIFF_SRVCES";
    public const string SheriffServicesHours = "SHERIFF_SRVCES_HOURS";

    public static readonly IReadOnlySet<string> Values = new HashSet<string>(StringComparer.Ordinal)
    {
        SheriffServices,
        SheriffServicesHours,
    };
}
