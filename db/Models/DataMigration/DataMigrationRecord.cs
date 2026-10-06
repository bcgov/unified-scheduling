namespace Unified.Db.Models.DataMigration;

public sealed class DataMigrationRecord
{
    public long Id { get; set; }
    public required string Source { get; set; }
    public required string EntityType { get; set; }
    public required string LegacyId { get; set; }
    public required string TargetType { get; set; }
    public required string TargetKey { get; set; }
    public DateTimeOffset? SourceUpdatedAtUtc { get; set; }
    public required string PayloadHash { get; set; }
    public Guid? LastAppliedRunId { get; set; }
    public bool IsDeleted { get; set; }
    public DataMigrationRun? LastAppliedRun { get; set; }
    public ICollection<DataMigrationMutation> Mutations { get; set; } = [];
}
