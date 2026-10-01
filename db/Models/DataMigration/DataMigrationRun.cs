namespace Unified.Db.Models.DataMigration;

public sealed class DataMigrationRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Source { get; set; }
    public required string Status { get; set; }
    public DateTimeOffset? StartedOn { get; set; }
    public DateTimeOffset? CompletedOn { get; set; }
    public DateTimeOffset? WatermarkStartedAt { get; set; }
    public DateTimeOffset? WatermarkCompletedAt { get; set; }
    public bool DryRun { get; set; }
    public int ScannedCount { get; set; }
    public int InsertedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int UnchangedCount { get; set; }
    public int FailedCount { get; set; }
    public int SkippedCount { get; set; }
    public int RejectedCount { get; set; }
    public Guid? RollbackOfRunId { get; set; }
    public ICollection<DataMigrationFailure> Failures { get; set; } = [];
    public ICollection<DataMigrationMutation> Mutations { get; set; } = [];
}
