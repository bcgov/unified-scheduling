namespace Unified.Db.Models.DataMigration;

// Separate simulation namespace: deliberately has no FK to applied business targets/crosswalks.
[Audit.EntityFramework.AuditIgnore]
public sealed class DataMigrationSimulationCandidate
{
    public Guid RunId { get; set; }
    public Guid NamespaceId { get; set; }
    public string Source { get; set; } = "SS";
    public required string EntityType { get; set; }
    public required string LegacyId { get; set; }
    public required string TargetType { get; set; }
    public required string TargetKey { get; set; }
    public required string PayloadHash { get; set; }
}
