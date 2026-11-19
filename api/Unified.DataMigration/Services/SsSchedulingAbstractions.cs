using Unified.DataMigration.Sources;
using Unified.Db.Models;
using Unified.Db.Models.Abstract;
using Unified.Db.Models.DataMigration;
using Unified.Db.Models.Scheduling;
using Unified.Db.Models.UserManagement;

namespace Unified.DataMigration.Services;

/// <summary>
/// The session operations the Scheduling rules (<see cref="SsSchedulingWriter"/>,
/// <see cref="SsAssignmentSeriesWriter"/>, <see cref="SsSchedulingGraphReplay"/>) use. PerRow binds
/// them to <see cref="SsWriteSession"/> (savepoints and EF saves); Bulk binds them to an in-memory
/// simulation of the same semantics, so both modes run the SAME rule code and reach identical outcomes.
/// </summary>
internal interface ISsSchedulingSession
{
    DataMigrationRun Run { get; }
    DateTimeOffset Now { get; }
    List<DataMigrationOutcome> Outcomes { get; }
    HashSet<(string Type, string Id)> Rebinding { get; }

    T Stamp<T>(T entity)
        where T : BaseEntity;

    void Outcome(string type, string id, string outcome, string? reason = null);
    DataMigrationOutcome? FindOutcome(string type, string id);
    int OutcomePosition(string type, string id);
    Task RowAsync(string type, SsRow source, Func<Task> write, CancellationToken ct);
    Task AtomicGraphAsync(
        string type,
        SsRow source,
        Func<Task> write,
        CancellationToken ct,
        IReadOnlyList<(string Type, string Id)>? members = null
    );
    Task GuardGraphAsync(IReadOnlyCollection<DataMigrationRecord> records, CancellationToken ct);
    Task SealNewDutyEntriesAsync(IReadOnlyCollection<int> ids, CancellationToken ct);
    Task PlanGraphAsync(Func<Task> plan, CancellationToken ct);
    Task<string> ResolveAsync(string type, string? id, CancellationToken ct);
    Task<int?> ResolveIntAsync(string type, int? id, CancellationToken ct);
    Task<bool> ReplayAsync(
        string type,
        SsRow source,
        string sourceHash,
        bool reference,
        CancellationToken ct,
        Func<Task>? applyOwnedUpdate = null
    );
    Task LinkAsync(
        string type,
        SsRow source,
        string targetType,
        object targetId,
        string hash,
        bool created,
        CancellationToken ct,
        string outcome = "Migrated",
        string? reason = null
    );
    Task<bool> ReplayGroupAsync(
        string type,
        IReadOnlyList<SsRow> members,
        string sourceHash,
        Func<string, Task> update,
        CancellationToken ct
    );
    Task ComposeAsync(string type, IReadOnlyCollection<string> ids, Func<Task> compose, CancellationToken ct);

    /// <summary>Called right before an existing graph is deleted and rewritten (PerRow only).</summary>
    void BeforeGraphReplacement();
}

/// <summary>
/// Target reads/writes the Scheduling rules perform on their main (first-apply and replay) path.
/// PerRow: <see cref="SsSchedulingEfStore"/> (the original EF queries, verbatim). Bulk: an in-memory
/// target model that enforces the same database constraints. Owned-update and graph-replacement
/// branches are PerRow-only and keep using the context directly (Bulk refuses or rejects them first).
/// </summary>
internal interface ISsSchedulingStore
{
    Task<List<string>> RetainedMemberIdsAsync(CancellationToken ct);
    Task<bool> ClassificationExistsAsync(int subcategory, int category, CancellationToken ct);
    Task<List<CourtRoom>> RoomsAsync(string code, int? location, CancellationToken ct);
    Task AddRoomAsync(CourtRoom room, CancellationToken ct);
    Task<List<DataMigrationRecord>> RecordsAsync(
        string type,
        IReadOnlyCollection<string> legacyIds,
        CancellationToken ct
    );
    Task<bool> DefinitionNameTakenAsync(int location, string normalized, int[] exceptIds, CancellationToken ct);
    Task AddDefinitionAsync(AssignmentDefinition definition, CancellationToken ct);
    Task<AssignmentDefinition> DefinitionAsync(int id, CancellationToken ct);
    Task<User> UserAsync(Guid id, CancellationToken ct);
    Task<bool> ShiftOnDayExistsAsync(
        int location,
        Guid user,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct
    );
    Task AddShiftAsync(ShiftEntry shift, CancellationToken ct);
    Task<List<(string LegacyId, string TargetKey)>> ResolvedShiftsAsync(string[] legacyIds, CancellationToken ct);
    Task AddAssignmentEntryAsync(AssignmentEntry entry, CancellationToken ct);
    Task<List<ShiftEntry>> OverlappingShiftsAsync(
        int location,
        Guid user,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken ct
    );
    Task<bool> FutureAssignmentConflictAsync(
        Guid user,
        int entryId,
        int definitionId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct
    );
    Task<ShiftAssignmentEntry?> ShiftAssignmentAsync(int shiftId, int entryId, CancellationToken ct);
    void AddShiftAssignment(ShiftAssignmentEntry link);
    void AddShiftAssignmentUser(ShiftAssignmentEntry link, ShiftAssignmentEntryUser user);
    Task SaveAsync(CancellationToken ct);
    Task<bool> AnyContainingShiftAsync(int[] ids, DateTimeOffset start, DateTimeOffset end, CancellationToken ct);
    Task<bool> RecordExistsAsync(string type, string legacyId, CancellationToken ct);
    Task<DataMigrationRecord?> RecordAsync(string type, string legacyId, CancellationToken ct);
    Task<DataMigrationOutcome?> PriorOutcomeAsync(Guid? runId, string type, string legacyId, CancellationToken ct);
    Task<AssignmentEntry> AssignmentEntryWithEventAsync(int id, CancellationToken ct);
    Task AddSeriesAsync(AssignmentSeries series, IReadOnlyList<AssignmentEntry> members, CancellationToken ct);
    Task<List<DataMigrationRecord>> SchedulingMembersAsync(CancellationToken ct);
    Task<List<DataMigrationRecord>> RootRecordsAsync(IReadOnlyCollection<string> types, CancellationToken ct);
    Task<List<DataMigrationRecord>> RecordsByIdAsync(long[] ids, CancellationToken ct);
    void Detach();
    Task FlushAndDetachAsync(CancellationToken ct);
    Task<bool> HasUnownedDependentsAsync(
        int[] definitions,
        int[] shifts,
        int[] entries,
        int[] series,
        CancellationToken ct
    );
}
