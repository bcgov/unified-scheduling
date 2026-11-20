namespace Unified.Db.Models.DataMigration;

public sealed class DataMigrationRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Source { get; set; }
    public string Job { get; set; } = "Legacy";
    public Guid? MyTeamRunId { get; set; }
    public DateOnly? CutoverDate { get; set; }
    public required string Status { get; set; }
    public DateTimeOffset? StartedOn { get; set; }
    public DateTimeOffset? CompletedOn { get; set; }
    public DateTimeOffset? HeartbeatOn { get; set; }
    public DateOnly? HistoryStartDate { get; set; }
    public int CommittedBatchCount { get; set; }
    public Guid? RollbackJournalKeyId { get; set; }
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

    // Recorded at acquisition so recovery can attribute a run even if dispatch never
    // reaches the separate operator-action audit record. Nulls identify older runs.
    public Guid? InitiatedByUserId { get; set; }
    public string? InitiationKind { get; set; }
    public string? PersistenceMode { get; set; }

    // A fingerprint of the reader's selected projection, not the entire source database.
    // The value carries its format version; a special value marks differing retry snapshots.
    public string? AuditSourceFingerprint { get; set; }
    public long? AuditSourceRowCount { get; set; }
    public ICollection<DataMigrationFailure> Failures { get; set; } = [];
    public ICollection<DataMigrationMutation> Mutations { get; set; } = [];
}
