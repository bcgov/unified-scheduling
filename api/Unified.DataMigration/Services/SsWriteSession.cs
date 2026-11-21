using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Unified.DataMigration.Sources;
using Unified.Db;
using Unified.Db.Models.Abstract;
using Unified.Db.Models.DataMigration;

namespace Unified.DataMigration.Services;

public sealed class SsReject(string code) : Exception(code);

/// <summary>One job transaction; failed rows use savepoints, never poison independent rows.</summary>
public sealed class SsWriteSession(UnifiedDbContext db, DataMigrationRun run) : ISsSchedulingSession
{
    /// <summary>Same session logic over a different storage backend (Scheduling Bulk simulation).</summary>
    internal SsWriteSession(UnifiedDbContext db, DataMigrationRun run, ISsSessionBackend backend)
        : this(db, run) => this.backend = backend;

    private ISsSessionBackend? backend;
    private ISsSessionBackend Store => backend ??= new SsEfSessionBackend(db, this);

    /// <summary>The storage backend (EF for PerRow).</summary>
    internal ISsSessionBackend Backend => Store;

    internal static IReadOnlySet<string> TargetFields => SafeTargetFields;

    public List<DataMigrationOutcome> Outcomes { get; } = [];

    // Internal members cannot implicitly implement the (internal) rules interface.
    HashSet<(string Type, string Id)> ISsSchedulingSession.Rebinding => Rebinding;

    DataMigrationOutcome? ISsSchedulingSession.FindOutcome(string type, string id) => FindOutcome(type, id);

    int ISsSchedulingSession.OutcomePosition(string type, string id) => OutcomePosition(type, id);

    Task ISsSchedulingSession.AtomicGraphAsync(
        string type,
        SsRow source,
        Func<Task> write,
        CancellationToken ct,
        IReadOnlyList<(string Type, string Id)>? members
    ) => AtomicGraphAsync(type, source, write, ct, members);

    Task ISsSchedulingSession.GuardGraphAsync(IReadOnlyCollection<DataMigrationRecord> records, CancellationToken ct) =>
        GuardGraphAsync(records, ct);

    Task ISsSchedulingSession.SealNewDutyEntriesAsync(IReadOnlyCollection<int> ids, CancellationToken ct) =>
        SealNewDutyEntriesAsync(ids, ct);

    Task ISsSchedulingSession.PlanGraphAsync(Func<Task> plan, CancellationToken ct) => PlanGraphAsync(plan, ct);

    void ISsSchedulingSession.BeforeGraphReplacement() => Store.BeforeOwnedUpdate("GraphReplacement");

    private static readonly HashSet<string> PrimaryTypes =
    [
        "Region",
        "Location",
        "Role",
        "User",
        "UserRole",
        "PositionType",
        "LeaveType",
        "SheriffActingRank",
        "SheriffAwayLocation",
        "SheriffLeave",
        "LookupSortOrder",
        "Training",
        "SheriffTraining",
        "SheriffStandardTraining",
        "CourtRoom",
        "AssignmentClassification",
        "Assignment",
        "Duty",
        "DutySlot",
        "Shift",
        "LookupCode",
    ];
    public IEnumerable<DataMigrationOutcome> SourceOutcomes => Outcomes.Where(x => PrimaryTypes.Contains(x.EntityType));

    internal static bool IsSourceType(string type) => PrimaryTypes.Contains(type);

    // Append-only between savepoints: nested rollback removes only new members.
    // Copying the full growing sets for every row makes large simulations quadratic.
    private sealed class TargetCounter
    {
        private readonly HashSet<string> members = [];
        private readonly List<string> journal = [];
        public int Count => journal.Count;

        /// <summary>Keys in first-add order (the committed prefix is stable between unit boundaries).</summary>
        public IReadOnlyList<string> Journal => journal;

        public void Add(string key)
        {
            if (members.Add(key))
                journal.Add(key);
        }

        public void Rollback(int checkpoint)
        {
            if (checkpoint < 0 || checkpoint > journal.Count)
                throw new InvalidOperationException("Invalid target counter checkpoint.");
            while (journal.Count > checkpoint)
            {
                members.Remove(journal[^1]);
                journal.RemoveAt(journal.Count - 1);
            }
        }
    }

    private readonly TargetCounter insertedTargets = new();
    private readonly TargetCounter updatedTargets = new();
    public int Inserted => insertedTargets.Count;
    public int Updated => updatedTargets.Count;

    /// <summary>Distinct inserted target keys in first-insert order (Bulk chunk accounting).</summary>
    internal IReadOnlyList<string> InsertedKeys => insertedTargets.Journal;

    /// <summary>Distinct updated target keys in first-update order (Bulk chunk accounting).</summary>
    internal IReadOnlyList<string> UpdatedKeys => updatedTargets.Journal;
    public int Linked { get; private set; }
    public Func<CancellationToken, Task>? AfterUnitAsync { get; set; }

    // (EntityType, LegacyId) is unique in Outcomes; the index stores each key's list position.
    // Outcomes only grows by Outcome(), shrinks by tail rollback and is rebuilt by graph planning.
    private readonly Dictionary<(string Type, string Id), int> outcomeIndex = [];
    private Dictionary<DataMigrationOutcome, (string Outcome, string? Reason)>? outcomeChanges;

    // Keys whose outcome may have changed since the last batch commit (opt-in by SsBatchProgress).
    private HashSet<(string Type, string Id)>? dirtyOutcomes;
    public DataMigrationRun Run => run;

    /// <summary>See <see cref="Options.MigrationJobOptions.PlanReplacementFamiliesOnly"/>.</summary>
    public bool PlanReplacementFamiliesOnly { get; set; }

    /// <summary>See <see cref="Options.MigrationJobOptions.PrefetchReplayRecords"/>.</summary>
    public bool PrefetchReplayRecords { get; set; }
    private SsReplayPrefetch? prefetch;

    /// <summary>Starts page prefetch for <paramref name="rows"/> (PerRow EF backend with the switch on only).</summary>
    public void BeginReplayPrefetch(IReadOnlyDictionary<string, List<SsRow>> rows)
    {
        EndReplayPrefetch();
        if (PrefetchReplayRecords && Store is SsEfSessionBackend)
            prefetch = new SsReplayPrefetch(db, rows);
    }

    public void EndReplayPrefetch()
    {
        if (prefetch is null)
            return;
        Console.WriteLine(
            $"SsReplayPrefetch Run={run.Id} Hits={prefetch.Hits} Fallbacks={prefetch.Fallbacks} Pages={prefetch.PageLoads}"
        );
        prefetch.Dispose();
        prefetch = null;
    }

    internal Guid? SimulationNamespace { get; set; }
    internal string? SimulationSchema { get; set; }

    internal Guid NewUserId(string legacyId) =>
        SimulationNamespace is { } ns
            ? new Guid(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes($"{ns:D}:User:{legacyId}")
                )[..16]
            )
            : Guid.NewGuid();

    public DateTimeOffset Now => run.StartedOn!.Value;
    private int atomicGraphDepth;
    internal HashSet<(string Type, string Id)> Rebinding { get; } = [];

    // A connected old/new dependency graph is indivisible, even when it exceeds
    // BatchSize. Inner row failures roll back the entire graph, not half a split.
    internal async Task AtomicGraphAsync(
        string type,
        SsRow source,
        Func<Task> write,
        CancellationToken ct,
        IReadOnlyList<(string Type, string Id)>? members = null
    )
    {
        var after = AfterUnitAsync;
        AfterUnitAsync = null;
        try
        {
            await RowAsync(
                type,
                source,
                async () =>
                {
                    atomicGraphDepth++;
                    try
                    {
                        await write();
                    }
                    finally
                    {
                        atomicGraphDepth--;
                    }
                },
                ct
            );
        }
        finally
        {
            AfterUnitAsync = after;
            Rebinding.Clear();
        }
        var result = FindOutcome(type, source.Id);
        if (result?.Outcome is "Rejected" or "Failed" && members is not null)
            foreach (var member in members)
                Outcome(member.Type, member.Id, result.Outcome, result.Reason);
        if (after is not null)
            await after(ct);
    }

    internal async Task GuardGraphAsync(IReadOnlyCollection<DataMigrationRecord> records, CancellationToken ct)
    {
        foreach (
            var group in records
                .GroupBy(x => (x.TargetType, x.TargetKey))
                .OrderBy(x => x.Key.TargetType)
                .ThenBy(x => x.Key.TargetKey)
        )
        {
            if (!group.Any(x => x.CreatedByRunId is not null))
                throw new SsReject("GraphRequiresOriginalOwnedTarget");
            foreach (var record in group)
                if (!await Store.HasVersionedMutationAsync(record.Id, ct))
                    throw new SsReject("MissingOriginalTargetVersionProof");
            await TargetVersionAsync(group.Key.TargetType, group.Key.TargetKey, true, ct);
            await Store.LockGraphChildrenAsync(group.Key.TargetType, group.Key.TargetKey, ct);
            var actual = await TargetFingerprintAsync(group.Key.TargetType, group.Key.TargetKey, ct);
            if (group.Any(x => x.AfterHash != actual))
                throw new SsReject("OwnedTargetDrift");
        }
    }

    internal async Task SealNewDutyEntriesAsync(IReadOnlyCollection<int> ids, CancellationToken ct)
    {
        foreach (var id in ids)
        {
            var key = id.ToString(CultureInfo.InvariantCulture);
            var aliases = await Store.AliasesAsync("AssignmentEntry", key, null, ct);
            if (!aliases.Any(x => x.EntityType == "DutyEntry" && x.CreatedByRunId == run.Id))
                throw new SsReject("GraphSealRequiresNewOwnedEntry");
            var hash = await TargetFingerprintAsync("AssignmentEntry", key, ct);
            foreach (var alias in aliases)
            {
                alias.AfterHash = hash;
                Store.AddMutation(
                    new DataMigrationMutation
                    {
                        RunId = run.Id,
                        DataMigrationRecordId = alias.Id,
                        Sequence = await Store.NextSequenceAsync(run.Id, ct),
                        Operation = "SealNewGraph",
                        AfterHash = hash,
                        Version = await TargetVersionAsync("AssignmentEntry", key, false, ct),
                    }
                );
                await Store.SaveAsync(ct);
            }
            await Store.SaveAsync(ct);
        }
    }

    internal async Task PlanGraphAsync(Func<Task> plan, CancellationToken ct)
    {
        var after = AfterUnitAsync;
        var prior = Outcomes.Select(x => (Item: x, x.Outcome, x.Reason)).ToList();
        var inserted = insertedTargets.Count;
        var updated = updatedTargets.Count;
        var linked = Linked;
        await Store.CreateSavepointAsync("ss_graph_plan", ct);
        AfterUnitAsync = null;
        try
        {
            await plan();
        }
        finally
        {
            await Store.RollbackSavepointAsync("ss_graph_plan");
            Store.ClearTracker();
            Outcomes.Clear();
            outcomeIndex.Clear();
            foreach (var item in prior)
            {
                item.Item.Outcome = item.Outcome;
                item.Item.Reason = item.Reason;
                outcomeIndex[(item.Item.EntityType, item.Item.LegacyId)] = Outcomes.Count;
                Outcomes.Add(item.Item);
            }
            insertedTargets.Rollback(inserted);
            updatedTargets.Rollback(updated);
            Linked = linked;
            AfterUnitAsync = after;
        }
    }

    internal static string ProposalHash(string type, SsRow source, string hash) => MappedSourceHash(type, source, hash);

    public static string Hash(object safeStructuralFields) =>
        DataMigrationOrchestrator.ComputePayloadHash(JsonSerializer.Serialize(safeStructuralFields));

    internal DataMigrationOutcome? FindOutcome(string type, string id) =>
        outcomeIndex.TryGetValue((type, id), out var position) ? Outcomes[position] : null;

    /// <summary>List position of the outcome for this key, or -1 when it has none.</summary>
    internal int OutcomePosition(string type, string id) =>
        outcomeIndex.TryGetValue((type, id), out var position) ? position : -1;

    internal void TrackOutcomeChanges()
    {
        dirtyOutcomes ??= [];
        foreach (var outcome in Outcomes)
            dirtyOutcomes.Add((outcome.EntityType, outcome.LegacyId));
    }

    /// <summary>Keys touched since the previous call (some may since have been rolled back).</summary>
    internal List<(string Type, string Id)> TakeChangedOutcomeKeys()
    {
        if (dirtyOutcomes is null)
            throw new InvalidOperationException("Outcome change tracking is not enabled.");
        var keys = dirtyOutcomes.ToList();
        dirtyOutcomes.Clear();
        return keys;
    }

    public void Outcome(string type, string id, string outcome, string? reason = null)
    {
        dirtyOutcomes?.Add((type, id));
        if (outcomeIndex.TryGetValue((type, id), out var position))
        {
            var prior = Outcomes[position];
            outcomeChanges?.TryAdd(prior, (prior.Outcome, prior.Reason));
            prior.Outcome = outcome;
            prior.Reason = reason;
        }
        else
        {
            var item = new DataMigrationOutcome
            {
                RunId = run.Id,
                EntityType = type,
                LegacyId = id,
                Outcome = outcome,
                Reason = reason,
            };
            outcomeIndex[(type, id)] = Outcomes.Count;
            Outcomes.Add(item);
        }
    }

    // Hash mapped text in memory; persist only the SHA-256 digest. Photo bytes NEVER
    // participate. A changed photo is gated by source timestamps and target row versions.
    private static string MappedSourceHash(string type, SsRow source, string baseHash)
    {
        object? text = type switch
        {
            "User" => new
            {
                IdirName = (source.Text("IdirName") ?? "").Trim().ToLowerInvariant(),
                IdirId = source.Guid("IdirId"),
                KeyCloakId = source.Guid("KeyCloakId"),
                FirstName = source.Text("FirstName") ?? "",
                LastName = source.Text("LastName") ?? "",
                Email = source.Text("Email") ?? "",
                Badge = source.Text("BadgeNumber"),
                Rank = source.Text("Rank"),
                PhotoStamp = source.Date("LastPhotoUpdate"),
                HasPhoto = source.Bytes("Photo") is not null,
                PhotoChangeStamp = source.Bytes("Photo") is null
                    ? null
                    : source.Date("UpdatedOn") ?? source.Date("CreatedOn"),
            },
            "UserRole" => new { Reason = source.Text("ExpiryReason") },
            "SheriffActingRank" or "SheriffAwayLocation" or "SheriffLeave" => new
            {
                Comment = source.Text("Comment"),
                Reason = source.Text("ExpiryReason"),
                Rank = type == "SheriffActingRank" ? source.Text("Rank") : null,
            },
            "SheriffTraining" => new { Comment = source.Text("Comment"), Note = source.Text("Note") },
            "Duty" => new { Comment = source.Text("Comment") },
            _ => null,
        };
        return text is null ? baseHash : Hash(new { baseHash, mappedText = text });
    }

    public T Stamp<T>(T entity)
        where T : BaseEntity
    {
        entity.CreatedOn = Now;
        // Unified audit identity, never source CreatedBy/UpdatedBy values.
        return entity;
    }

    public async Task RowAsync(string type, SsRow source, Func<Task> write, CancellationToken ct)
    {
        Store.EnsureTransaction();
        var checkpoint = Outcomes.Count;
        var inserted = insertedTargets.Count;
        var updated = updatedTargets.Count;
        var linked = Linked;
        var parentChanges = outcomeChanges;
        var changes = new Dictionary<DataMigrationOutcome, (string Outcome, string? Reason)>();
        outcomeChanges = changes;
        await Store.CreateSavepointAsync("ss_row", ct);
        try
        {
            await write();
            await Store.SaveAsync(ct);
            await Store.ReleaseSavepointAsync("ss_row", ct);
            // A successful inner savepoint is still provisional until its
            // enclosing graph commits. Preserve the earliest outcome image
            // so a later sibling failure restores pre-graph counters too.
            if (parentChanges is not null)
                foreach (var change in changes)
                    parentChanges.TryAdd(change.Key, change.Value);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (SsBulkRefusal)
        {
            // Bulk cannot reproduce this path exactly; abort the whole simulation.
            throw;
        }
        catch (Exception ex)
        {
            await Store.RollbackSavepointAsync("ss_row");
            Store.ClearTracker();
            // Keys are unique, so dropping the removed tail from the index is the same as
            // rebuilding it from the surviving prefix (whose positions do not move).
            for (var i = checkpoint; i < Outcomes.Count; i++)
                outcomeIndex.Remove((Outcomes[i].EntityType, Outcomes[i].LegacyId));
            Outcomes.RemoveRange(checkpoint, Outcomes.Count - checkpoint);
            foreach (var change in changes)
            {
                change.Key.Outcome = change.Value.Outcome;
                change.Key.Reason = change.Value.Reason;
                dirtyOutcomes?.Add((change.Key.EntityType, change.Key.LegacyId));
            }
            insertedTargets.Rollback(inserted);
            updatedTargets.Rollback(updated);
            Linked = linked;
            if (
                Store.PerRowDiagnostics
                && Environment.GetEnvironmentVariable("DataMigrationRehearsal__SafeStack") == "true"
                && db.Database.GetDbConnection().Database == "unified_ss_rehearsal_repartition_v4"
            )
            {
                Console.WriteLine("SafeRowFailure=" + ex.GetType().Name);
                if (ex.InnerException is PostgresException pg)
                    Console.WriteLine("SafeSqlState=" + pg.SqlState);
                foreach (
                    var frame in new System.Diagnostics.StackTrace(ex, true)
                        .GetFrames()
                        .Where(x =>
                            x.GetMethod()?.DeclaringType?.Namespace?.StartsWith("Unified.DataMigration") == true
                        )
                        .Take(4)
                )
                    Console.WriteLine(
                        $"SafeRowFrame={frame.GetMethod()?.DeclaringType?.Name}.{frame.GetMethod()?.Name}:{frame.GetFileLineNumber()}"
                    );
            }
            if (atomicGraphDepth > 0)
            {
                outcomeChanges = parentChanges;
                throw;
            }
            Outcome(
                type,
                source.Id,
                ex is SsReject ? "Rejected" : "Failed",
                ex is SsReject rejection ? rejection.Message : $"TargetWriteFailed:{ex.GetType().Name}"
            );
        }
        outcomeChanges = parentChanges;
        // Outside the row savepoint catch: checkpoint/commit failures must stop the
        // worker, not be misreported as a rejected business row.
        if (AfterUnitAsync is not null)
            await AfterUnitAsync(ct);
    }

    public async Task<string> ResolveAsync(string type, string? id, CancellationToken ct)
    {
        if (id is null)
            throw new SsReject("MissingParent");
        if (type == "User" && id == "00000000-0000-0000-0000-000000000001")
        {
            if (await Store.UserExistsAsync(new Guid(id), ct))
                return id;
            throw new SsReject("MissingUnifiedSystemUser");
        }
        if (prefetch is not null)
        {
            var (known, cached) = await prefetch.TryGetAsync(type, id, false, ct);
            if (known)
                return cached?.TargetKey ?? throw new SsReject("MissingParent");
        }
        return await Store.TargetKeyAsync(type, id, ct) ?? throw new SsReject("MissingParent");
    }

    // Prefetched answer of ReplayGroupAsync's no-write paths: false = no crosswalk yet, true = unchanged group,
    // null = the per-row query must decide (partial, changed, split or unknown members).
    private async Task<bool?> UnchangedGroupAsync(
        string type,
        IReadOnlyList<SsRow> members,
        string sourceHash,
        CancellationToken ct
    )
    {
        var entries = new List<SsReplayPrefetch.Entry>(members.Count);
        foreach (var member in members)
        {
            var (known, cached) = await prefetch!.TryGetAsync(type, member.Id, true, ct);
            if (!known)
                return null;
            if (cached is not null)
                entries.Add(cached);
        }
        if (entries.Count == 0)
            return false;
        return
            entries.Count == members.Count
            && entries.All(x => x.PayloadHash == sourceHash && x.TargetKey == entries[0].TargetKey)
            ? true
            : null;
    }

    public async Task<int?> ResolveIntAsync(string type, int? id, CancellationToken ct) =>
        id is null
            ? null
            : int.Parse(
                await ResolveAsync(type, id.Value.ToString(CultureInfo.InvariantCulture), ct),
                CultureInfo.InvariantCulture
            );

    public async Task<bool> ReplayAsync(
        string type,
        SsRow source,
        string sourceHash,
        bool reference,
        CancellationToken ct,
        Func<Task>? applyOwnedUpdate = null
    )
    {
        if (Rebinding.Contains((type, source.Id)))
            return false;
        sourceHash = MappedSourceHash(type, source, sourceHash);
        if (prefetch is not null)
        {
            var (known, cached) = await prefetch.TryGetAsync(type, source.Id, true, ct);
            if (known && cached is null)
                return false;
            // Only the no-write verdict is answered from the page; anything that writes reloads the tracked record.
            if (
                known
                && (
                    reference
                    || cached!.PayloadHash == sourceHash
                        && cached.SourceUpdatedAtUtc == (source.Date("UpdatedOn") ?? source.Date("CreatedOn"))
                )
            )
            {
                Outcome(type, source.Id, "Migrated", "ExistingCrosswalk");
                return true;
            }
        }
        var record = await Store.RecordAsync(type, source.Id, ct);
        if (record is null)
            return false;
        // References are add/link-only, including retired rows. Never refresh linked business rows.
        if (
            !reference
            && (
                record.PayloadHash != sourceHash
                || record.SourceUpdatedAtUtc != (source.Date("UpdatedOn") ?? source.Date("CreatedOn"))
            )
        )
        {
            if (record.PayloadHash == sourceHash)
            {
                record.SourceUpdatedAtUtc = source.Date("UpdatedOn") ?? source.Date("CreatedOn");
                record.LastAppliedRunId = run.Id;
                Outcome(type, source.Id, "Migrated", "UnchangedMappedSource");
                return true;
            }
            if (record.CreatedByRunId is null)
                throw new SsReject("LinkedOnlyTargetCannotUpdate");
            if (applyOwnedUpdate is null)
                throw new SsReject("OwnedUpdateRequiresReview");
            var prior = await Store.LatestMutationAsync(record.Id, ct);
            if (prior?.Version is null)
                throw new SsReject("MissingOriginalTargetVersionProof");
            var version = await TargetVersionAsync(record.TargetType, record.TargetKey, true, ct);
            // Mapped-value equality under row/event locks is the QX4 guard.
            // User additionally needs exact xmin: photo bytes are deliberately
            // excluded from hashes. Audit-only xmin changes on other targets
            // (including a verified inverse) must not permanently block replay.
            if (
                record.TargetType == "User" && version != prior.Version
                || record.AfterHash != await TargetFingerprintAsync(record.TargetType, record.TargetKey, ct)
            )
                throw new SsReject("OwnedTargetDrift");
            Store.BeforeOwnedUpdate($"OwnedUpdate:{type}");
            await applyOwnedUpdate();
            await Store.SaveAsync(ct);
            record.PayloadHash = sourceHash;
            record.AfterHash = await TargetFingerprintAsync(record.TargetType, record.TargetKey, ct);
            record.SourceUpdatedAtUtc = source.Date("UpdatedOn") ?? source.Date("CreatedOn");
            record.LastAppliedRunId = run.Id;
            Store.AddMutation(
                new DataMigrationMutation
                {
                    RunId = run.Id,
                    DataMigrationRecordId = record.Id,
                    Sequence = await Store.NextSequenceAsync(run.Id, ct),
                    Operation = "Update",
                    AfterHash = record.AfterHash,
                    Version = await TargetVersionAsync(record.TargetType, record.TargetKey, false, ct),
                }
            );
            await Store.SaveAsync(ct);
            var aliases = await Store.AliasesAsync(record.TargetType, record.TargetKey, record.Id, ct);
            foreach (var alias in aliases)
            {
                alias.AfterHash = record.AfterHash;
                Store.AddMutation(
                    new DataMigrationMutation
                    {
                        RunId = run.Id,
                        DataMigrationRecordId = alias.Id,
                        Sequence = await Store.NextSequenceAsync(run.Id, ct),
                        Operation = "GraphRefresh",
                        AfterHash = record.AfterHash!,
                        Version = await TargetVersionAsync(record.TargetType, record.TargetKey, false, ct),
                    }
                );
                await Store.SaveAsync(ct);
            }
            updatedTargets.Add($"{record.TargetType}:{record.TargetKey}");
            Outcome(type, source.Id, "Migrated", "OwnedMappedUpdate");
            return true;
        }
        Outcome(type, source.Id, "Migrated", "ExistingCrosswalk");
        return true;
    }

    public async Task LinkAsync(
        string type,
        SsRow source,
        string targetType,
        object targetId,
        string hash,
        bool created,
        CancellationToken ct,
        string outcome = "Migrated",
        string? reason = null
    )
    {
        var owner = type switch
        {
            "Region"
            or "Location"
            or "PositionType"
            or "LeaveType"
            or "Role"
            or "User"
            or "UserRole"
            or "SheriffActingRank"
            or "SheriffLeave"
            or "SheriffAwayLocation" => "MyTeam",
            "Training" or "SheriffTraining" => "Training",
            "CourtRoom"
            or "AssignmentClassification"
            or "Assignment"
            or "AssignmentSeries"
            or "DutyEntry"
            or "SchedulingComponent"
            or "SchedulingMember"
            or "Shift"
            or "Duty"
            or "DutySlot" => "Scheduling",
            _ => throw new SsReject("UnknownEntityOwnership"),
        };
        if (owner != run.Job)
            throw new SsReject("EntityOwnershipViolation");
        var afterHash = await TargetFingerprintAsync(targetType, targetId, ct);
        var record = Rebinding.Contains((type, source.Id))
            ? await Store.RequiredRecordAsync(type, source.Id, ct)
            : null;
        var rebinding = record is not null;
        record ??= new DataMigrationRecord
        {
            Source = "SS",
            EntityType = type,
            LegacyId = source.Id,
            TargetType = targetType,
            TargetKey = Convert.ToString(targetId, CultureInfo.InvariantCulture)!,
            PayloadHash = MappedSourceHash(type, source, hash),
            AfterHash = afterHash,
            CreatedByRunId = created ? run.Id : null,
            SourceUpdatedAtUtc = source.Date("UpdatedOn") ?? source.Date("CreatedOn"),
            LastAppliedRunId = run.Id,
        };
        if (rebinding)
        {
            record.TargetType = targetType;
            record.TargetKey = Convert.ToString(targetId, CultureInfo.InvariantCulture)!;
            record.PayloadHash = MappedSourceHash(type, source, hash);
            record.AfterHash = afterHash;
            record.CreatedByRunId = created ? run.Id : null;
            record.SourceUpdatedAtUtc = source.Date("UpdatedOn") ?? source.Date("CreatedOn");
            record.LastAppliedRunId = run.Id;
            Rebinding.Remove((type, source.Id));
        }
        else
            Store.AddRecord(record);
        await Store.SaveAsync(ct);
        Store.AddMutation(
            new DataMigrationMutation
            {
                RunId = run.Id,
                DataMigrationRecordId = record.Id,
                Sequence = await Store.NextSequenceAsync(run.Id, ct),
                Operation =
                    rebinding ? "Repartition"
                    : created ? "Insert"
                    : "Link",
                AfterHash = afterHash,
                Version = await TargetVersionAsync(targetType, targetId, false, ct),
            }
        );
        if (created)
            insertedTargets.Add($"{targetType}:{record.TargetKey}");
        else
            Linked++;
        Outcome(type, source.Id, outcome, reason);
    }

    public async Task<bool> ReplayGroupAsync(
        string type,
        IReadOnlyList<SsRow> members,
        string sourceHash,
        Func<string, Task> update,
        CancellationToken ct
    )
    {
        var ids = members.Select(x => x.Id).ToArray();
        if (ids.Any(id => Rebinding.Contains((type, id))))
            return false;
        if (prefetch is not null && await UnchangedGroupAsync(type, members, sourceHash, ct) is { } unchanged)
        {
            if (!unchanged)
                return false;
            foreach (var member in members)
                Outcome(type, member.Id, members.Count > 1 ? "Merged" : "Migrated", "ExistingGroupCrosswalk");
            return true;
        }
        var records = await Store.RecordsAsync(type, ids, ct);
        if (records.Count == 0)
            return false;
        if (records.Select(x => x.TargetKey).Distinct().Count() != 1)
            throw new SsReject("GroupMergeRequiresMultiTargetRepartition");
        var owner = records[0];
        if (records.Count == members.Count && records.All(x => x.PayloadHash == sourceHash))
        {
            foreach (var member in members)
                Outcome(type, member.Id, members.Count > 1 ? "Merged" : "Migrated", "ExistingGroupCrosswalk");
            return true;
        }
        if (records.Any(x => x.CreatedByRunId is null))
            throw new SsReject("LinkedOnlyTargetCannotUpdate");
        if (await Store.GroupHasOtherMemberAsync(type, owner.TargetKey, ids, ct))
            throw new SsReject("GroupSplitRequiresMultiTargetRepartition");
        var prior = await Store.LatestMutationAsync(owner.Id, ct);
        if (prior?.Version is null)
            throw new SsReject("MissingOriginalTargetVersionProof");
        var version = await TargetVersionAsync(owner.TargetType, owner.TargetKey, true, ct);
        if (
            owner.TargetType == "User" && version != prior.Version
            || await TargetFingerprintAsync(owner.TargetType, owner.TargetKey, ct) != owner.AfterHash
            || records.Any(x => x.AfterHash != owner.AfterHash)
        )
            throw new SsReject("OwnedTargetDrift");
        Store.BeforeOwnedUpdate($"OwnedGroupUpdate:{type}");
        await update(owner.TargetKey);
        await Store.SaveAsync(ct);
        var afterHash = await TargetFingerprintAsync(owner.TargetType, owner.TargetKey, ct);
        var afterVersion = await TargetVersionAsync(owner.TargetType, owner.TargetKey, false, ct);
        foreach (var member in members)
        {
            var record = records.SingleOrDefault(x => x.LegacyId == member.Id);
            if (record is null)
            {
                await LinkAsync(type, member, owner.TargetType, owner.TargetKey, sourceHash, false, ct);
                record = await Store.RequiredRecordAsync(type, member.Id, ct);
                record.CreatedByRunId = owner.CreatedByRunId;
            }
            record.PayloadHash = sourceHash;
            record.AfterHash = afterHash;
            record.SourceUpdatedAtUtc = member.Date("UpdatedOn") ?? member.Date("CreatedOn");
            record.LastAppliedRunId = run.Id;
            Store.AddMutation(
                new DataMigrationMutation
                {
                    RunId = run.Id,
                    DataMigrationRecordId = record.Id,
                    Sequence = await Store.NextSequenceAsync(run.Id, ct),
                    Operation = "UpdateGroup",
                    AfterHash = afterHash,
                    Version = afterVersion,
                }
            );
            await Store.SaveAsync(ct);
            Outcome(type, member.Id, members.Count > 1 ? "Merged" : "Migrated", "OwnedFullGroupUpdate");
        }
        updatedTargets.Add($"{owner.TargetType}:{owner.TargetKey}");
        return true;
    }

    public async Task ComposeAsync(
        string type,
        IReadOnlyCollection<string> ids,
        Func<Task> compose,
        CancellationToken ct
    )
    {
        if (!(run.Job == "Scheduling" && type == "Duty" || run.Job == "Training" && type == "User"))
            throw new SsReject("EntityOwnershipViolation");
        var records = await Store.RecordsAsync(type, ids, ct);
        if (records.Count != ids.Distinct().Count() || records.Any(x => x.CreatedByRunId is null))
            throw new SsReject("CompositionRequiresOwnedTargets");
        foreach (var record in records.OrderBy(x => x.TargetKey, StringComparer.Ordinal))
        {
            var prior = await Store.LatestMutationAsync(record.Id, ct);
            var version = await TargetVersionAsync(record.TargetType, record.TargetKey, true, ct);
            if (
                prior?.Version is null
                || record.TargetType == "User" && version != prior.Version
                || await TargetFingerprintAsync(record.TargetType, record.TargetKey, ct) != record.AfterHash
            )
                throw new SsReject("OwnedTargetDrift");
        }

        await compose();
        await Store.SaveAsync(ct);
        foreach (var record in records)
        {
            var hash = await TargetFingerprintAsync(record.TargetType, record.TargetKey, ct);
            if (type == "User" && hash != record.AfterHash)
                throw new SsReject("TrainingCannotChangeMyTeamMappedFields");
            var version = await TargetVersionAsync(record.TargetType, record.TargetKey, false, ct);
            var aliases = await Store.AliasesAsync(record.TargetType, record.TargetKey, null, ct);
            foreach (var alias in aliases)
            {
                alias.AfterHash = hash;
                Store.AddMutation(
                    new DataMigrationMutation
                    {
                        RunId = run.Id,
                        DataMigrationRecordId = alias.Id,
                        Sequence = await Store.NextSequenceAsync(run.Id, ct),
                        Operation = "Compose",
                        AfterHash = hash,
                        Version = version,
                    }
                );
                await Store.SaveAsync(ct);
            }
        }
    }

    internal async Task RefreshRestoredReceiptsAsync(
        IReadOnlyCollection<(string Type, string Key)> targets,
        CancellationToken ct
    )
    {
        var records = await db.DataMigrationRecords.Where(x => x.Source == "SS").ToListAsync(ct);
        foreach (var record in records.Where(x => targets.Contains((x.TargetType, x.TargetKey))))
        {
            // Preserve independent drift rather than blessing another owner's edit.
            if (await TargetFingerprintAsync(record.TargetType, record.TargetKey, ct) != record.AfterHash)
                continue;
            db.DataMigrationMutations.Add(
                new DataMigrationMutation
                {
                    RunId = run.Id,
                    DataMigrationRecordId = record.Id,
                    Sequence =
                        (
                            await db
                                .DataMigrationMutations.Where(x => x.RunId == run.Id)
                                .Select(x => (long?)x.Sequence)
                                .MaxAsync(ct)
                            ?? 0
                        ) + 1,
                    Operation = "RollbackRefresh",
                    AfterHash = record.AfterHash!,
                    Version = await TargetVersionAsync(record.TargetType, record.TargetKey, false, ct),
                }
            );
            await db.SaveChangesAsync(ct);
        }
    }

    private static readonly HashSet<string> SafeTargetFields =
    [
        "Name",
        "NormalizedName",
        "Description",
        "Title",
        "Notes",
        "Comment",
        "ExpiryReason",
        "CancellationReason",
        "CancelledReason",
        "FirstName",
        "LastName",
        "Email",
        "IdirName",
        "IdirId",
        "KeyCloakId",
        "BadgeNumber",
        "Rank",
        "EmployeeNumber",
        "SourceModule",
        "Id",
        "JustinId",
        "AgencyId",
        "JustinLocationCode",
        "ParentLocationId",
        "RegionId",
        "Timezone",
        "ExpiryDate",
        "EffectiveDate",
        "Code",
        "DisplayOrder",
        "Order",
        "IsPaid",
        "IsEnabled",
        "Gender",
        "HomeLocationId",
        "PendingRegistration",
        "LastLogin",
        "LastPhotoUpdate",
        "UserId",
        "RoleId",
        "StartAtUtc",
        "EndAtUtc",
        "ExpiryAtUtc",
        "PositionTypeId",
        "Mandatory",
        "ValidityDays",
        "AdvanceNoticeDays",
        "Rotating",
        "TrainingCategoryId",
        "TrainingId",
        "Version",
        "AwardedOn",
        "EndingOn",
        "AllDay",
        "NoticeState",
        "LeaveTypeId",
        "EventId",
        "LocationId",
        "LoanOutLocationId",
        "CategoryId",
        "SubCategoryId",
        "EffectiveDateUtc",
        "ExpiryDateUtc",
        "DefaultCapacity",
        "DefaultStartTime",
        "DefaultEndTime",
        "LunchAvailableMinutes",
        "WorkedLunchMinutes",
        "AssignmentDefinitionId",
        "AssignmentSeriesId",
        "EventSeriesId",
        "Capacity",
        "TimeZoneId",
        "StatusTypeCode",
        "EventTypeCode",
        "CancelledAt",
        "RecurrenceRule",
    ];

    private Task<string> TargetVersionAsync(string targetType, object targetId, bool locked, CancellationToken ct) =>
        Store.TargetVersionAsync(targetType, targetId, locked, ct);

    private Task<string> TargetFingerprintAsync(string targetType, object targetId, CancellationToken ct) =>
        Store.TargetFingerprintAsync(targetType, targetId, ct);

    public static string? Timezone(SsRow source, bool required)
    {
        var zone = source.Text("Timezone");
        if (zone is null && !required)
            return null;
        if (string.IsNullOrWhiteSpace(zone))
            throw new SsReject("InvalidTimezone");
        try
        {
            if (!TimeZoneInfo.FindSystemTimeZoneById(zone).HasIanaId)
                throw new SsReject("InvalidTimezone");
        }
        catch (TimeZoneNotFoundException)
        {
            throw new SsReject("InvalidTimezone");
        }
        catch (InvalidTimeZoneException)
        {
            throw new SsReject("InvalidTimezone");
        }
        return zone;
    }

    public static (DateTimeOffset Start, DateTimeOffset End) Dates(SsRow source)
    {
        var start = source.Date("StartDate") ?? throw new SsReject("MissingStart");
        var end = source.Date("EndDate") ?? throw new SsReject("MissingEnd");
        if (end < start)
            throw new SsReject("ReversedDates");
        return (start, end);
    }
}
