namespace Unified.Db.Models.DataMigration;

// Simulation manifests only: never business rows, private text, photos or applied crosswalks.
[Audit.EntityFramework.AuditIgnore]
public sealed class DataMigrationSimulation
{
    public Guid RunId { get; set; }
    public Guid NamespaceId { get; set; }
    public Guid MyTeamRunId { get; set; }
    public required string SourceHash { get; set; }
    public required string BaselineHash { get; set; }
    public required string ConfigurationHash { get; set; }
    public required string CandidateHash { get; set; }
    public int CandidateCount { get; set; }
    public int FormatVersion { get; set; } = 2;
    public ICollection<DataMigrationSimulationCandidate> Candidates { get; set; } = [];
}
