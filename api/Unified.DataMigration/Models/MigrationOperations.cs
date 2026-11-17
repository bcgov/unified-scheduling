namespace Unified.DataMigration.Models;

public sealed record MigrationRunDto(
    Guid Id,
    string Source,
    string Job,
    string Status,
    string Verdict,
    bool DryRun,
    Guid? MyTeamRunId,
    Guid? RollbackOfRunId,
    DateOnly? CutoverDate,
    DateOnly? HistoryStartDate,
    DateTimeOffset? StartedOn,
    DateTimeOffset? CompletedOn,
    DateTimeOffset? HeartbeatOn,
    int Scanned,
    int Inserted,
    int Updated,
    int Unchanged,
    int Skipped,
    int Rejected,
    int Failed,
    int CommittedBatches
);

public sealed record MigrationPage<T>(IReadOnlyList<T> Items, int Offset, int Limit, int Total);

public sealed record MigrationFindingDto(
    long Id,
    string Kind,
    string Entity,
    string Key,
    string Outcome,
    string Code,
    DateTimeOffset? OccurredOn
);

public sealed record MigrationControlDto(string Scope, bool Paused, bool AbortRequested, DateTimeOffset? ChangedOn);

public sealed record MigrationStatusDto(
    string Source,
    string Job,
    bool Enabled,
    bool DryRun,
    int BatchSize,
    DateOnly? CutoverDate,
    DateOnly HistoryStartDate,
    MigrationControlDto SourceControl,
    MigrationControlDto JobControl
);

public sealed record MigrationTriggerRequest(bool DryRun = true);

public sealed record MigrationAcceptedDto(Guid RunId, string Source, string Job, bool DryRun);

public sealed record MigrationRecoveryRequest(DateTimeOffset StaleBeforeUtc);

public sealed record MigrationRollbackRequest(Guid ConfirmDataLoss, string Digest);

public sealed record MigrationRollbackPreviewDto(
    Guid RunId,
    string Digest,
    int Rows,
    int Dependents,
    string[] Blockers,
    string ImpactDownload
);

public sealed record MigrationRollbackReceiptDto(Guid RunId, Guid RollbackRunId);

public sealed class MigrationOperationException(int status, string code) : Exception(code)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}
