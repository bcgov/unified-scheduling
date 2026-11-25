using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
using Unified.Audit.Options;
using Unified.DataMigration.Sources;
using Unified.Db;
using Unified.Db.Models.DataMigration;
using Unified.Db.Models.Training;
using Unified.Db.Models.UserManagement;

namespace Unified.DataMigration.Services.Bulk;

/// <summary>
/// Opt-in Bulk persistence for the SS Training job (<c>PersistenceMode=Bulk</c>). Classification and
/// mapping reuse <see cref="SsTrainingRules"/>; every PerRow rejection/outcome rule is simulated in memory
/// against target state loaded under table locks, then accepted rows are persisted per chunk with binary
/// COPY staging and set-based SQL. Chunks never split a user (or users linked by an owned rebind), so
/// per-user version numbering, compose and drift decisions match the PerRow writer.
/// No rollback journal is captured; see <see cref="SsBulkUndo"/>.
/// </summary>
public sealed class SsTrainingBulkWriter(UnifiedDbContext db, IOptions<AuditRecordOptions>? auditOptions = null)
{
    private const string SystemUserKey = "00000000-0000-0000-0000-000000000001";

    /// <summary>Test hook invoked inside each chunk transaction just before commit (chunk index).</summary>
    internal Func<int, CancellationToken, Task>? BeforeChunkCommitAsync { get; set; }

    internal static string? OwnedType(string table, SsRow row) =>
        table switch
        {
            "LookupCode" => row.Int("Type") == 6 ? "Training" : null,
            "SheriffTraining" or "SheriffStandardTraining" => table,
            _ => null,
        };

    private sealed class RowFailure(string type) : Exception(type);

    private sealed class Ut
    {
        public int Id { get; set; }
        public Guid UserId { get; set; }
        public int TrainingId { get; set; }
        public int Version { get; set; }
        public DateTimeOffset EndingOn { get; set; }
        public DateTimeOffset AwardedOn { get; set; }
    }

    private sealed record Rec(
        long Id,
        string LegacyId,
        string TargetType,
        string TargetKey,
        string PayloadHash,
        string? AfterHash,
        Guid? CreatedByRunId,
        DateTimeOffset? SourceUpdatedAtUtc
    );

    private sealed record HistoryWrite(
        Ut State,
        SsRow Row,
        string MappedHash,
        DateTimeOffset AwardedOn,
        DateTimeOffset EndingOn,
        DateTimeOffset? ExpiryDate,
        string? Notes,
        string NoticeState,
        Rec? Record,
        bool VersionChanged
    );

    private sealed record Intent(string Kind, int Index);

    internal async Task ExecuteAsync(
        DataMigrationRun run,
        Dictionary<string, List<SsRow>> rows,
        DateOnly history,
        bool includeExcused,
        int chunkSize,
        Func<CancellationToken, Task> fence,
        CancellationToken ct
    )
    {
        if (run.Job != "Training" || run.DryRun || run.StartedOn is null)
            throw new InvalidOperationException("SS Training bulk persistence requires an applied Training run.");
        if (chunkSize <= 0)
            throw new InvalidOperationException("SS Training bulk chunk size must be positive.");
        var ledger = new SsBulkLedger(db, run, rows, history, OwnedType);
        var runner = new SsBulkChunkRunner(db, ledger, fence, BeforeChunkCommitAsync);
        var context = new Context(
            db,
            run,
            rows,
            includeExcused,
            ledger,
            auditOptions?.Value ?? new AuditRecordOptions()
        );
        await runner.RunAsync((connection, token) => context.WriteCoursesAsync(connection, token), ct);
        foreach (var chunk in await context.PlanHistoryChunksAsync(chunkSize, ct))
            await runner.RunAsync((connection, token) => context.WriteHistoriesAsync(connection, chunk, token), ct);
        await runner.RunAsync(
            (_, _) =>
            {
                foreach (var row in rows["SheriffStandardTraining"])
                    ledger.Outcome(
                        "SheriffStandardTraining",
                        row.Id,
                        "Excluded",
                        "UnifiedMandatoryPolicyReplacesFutureRules"
                    );
                return Task.CompletedTask;
            },
            ct,
            final: true
        );
    }

    private sealed class Context(
        UnifiedDbContext db,
        DataMigrationRun run,
        Dictionary<string, List<SsRow>> rows,
        bool includeExcused,
        SsBulkLedger ledger,
        AuditRecordOptions auditOptions
    )
    {
        private readonly DateTimeOffset now = run.StartedOn!.Value;
        private readonly Guid? actor = SsBulkTargets.StampActor(db);
        private readonly Dictionary<string, SsRow> users = rows["User"].ToDictionary(x => x.Id);
        private readonly HashSet<int?> shortenedCarbine = SsTrainingRules.ShortenedCarbineCourseIds(rows["LookupCode"]);

        private NpgsqlConnection Connection => SsBulkStaging.Connection(db);

        private Task<List<T>> QueryAsync<T>(
            string sql,
            Func<NpgsqlDataReader, T> map,
            CancellationToken ct,
            params NpgsqlParameter[] parameters
        ) => SsBulkStaging.QueryAsync(Connection, sql, map, ct, parameters);

        private Task<int> ExecuteAsync(string sql, CancellationToken ct, params NpgsqlParameter[] parameters) =>
            SsBulkStaging.ExecuteAsync(Connection, sql, ct, parameters);

        private async Task<List<int>> ActiveCarbineProfilesAsync(CancellationToken ct) =>
            await QueryAsync(
                """
                SELECT "Id" FROM "TrainingProfileTypes"
                WHERE "Code"='CARBINE_OPERATOR' AND "EffectiveDate"<=@now AND ("ExpiryDate" IS NULL OR "ExpiryDate">@now)
                """,
                x => x.GetInt32(0),
                ct,
                new NpgsqlParameter("now", now)
            );

        private void Simulate(string type, SsRow row, Action body)
        {
            try
            {
                body();
            }
            catch (SsReject reject)
            {
                ledger.Outcome(type, row.Id, "Rejected", reject.Message);
            }
            catch (RowFailure failure)
            {
                ledger.Outcome(type, row.Id, "Failed", $"TargetWriteFailed:{failure.Message}");
            }
        }

        /// <summary>
        /// PerRow surfaces an oversized mapped note as a failed save of that row (savepoint rolled back,
        /// <c>TargetWriteFailed:DbUpdateException</c>); Bulk reports the same row-level outcome instead of
        /// failing the whole chunk at COPY time.
        /// </summary>
        private static void EnsureNotesFit(string? notes)
        {
            if (NotesTooLong(notes))
                throw new RowFailure(nameof(DbUpdateException));
        }

        // PostgreSQL varchar(n) counts characters (code points), not UTF-16 code units, and accepts an
        // over-length value whose excess characters are all ASCII spaces (stored truncated to n), as PerRow does.
        private static bool NotesTooLong(string? notes)
        {
            const int max = SsTrainingRules.UserTrainingNotesMaxLength;
            if (notes is null || notes.Length <= max)
                return false;
            var count = 0;
            var index = 0;
            foreach (var rune in notes.EnumerateRunes())
            {
                if (count == max)
                    break;
                count++;
                index += rune.Utf16SequenceLength;
            }
            return count == max && notes.AsSpan(index).ContainsAnyExcept(' ');
        }

        // ---------------------------------------------------------------- courses (chunk 0)

        private sealed record NewCourse(
            SsRow Row,
            string Code,
            SsTrainingRules.CourseMapping Mapping,
            bool Rotating,
            string? CategoryName,
            int? CategoryId,
            int Order,
            int? Profile
        );

        public async Task WriteCoursesAsync(NpgsqlConnection connection, CancellationToken ct)
        {
            await ExecuteAsync("LOCK TABLE \"Trainings\" IN SHARE ROW EXCLUSIVE MODE", ct);
            var lookup = rows["LookupCode"];
            var sourceCodes = SsTrainingRules.SourceCourseCodes(lookup);
            var courseCodeCounts = SsTrainingRules.CourseCodeCounts(lookup);
            var repeats = SsTrainingRules.RepeatedCourses(rows["SheriffTraining"], users, includeExcused);
            var linked = (
                await QueryAsync(
                    "SELECT \"LegacyId\" FROM \"DataMigrationRecords\" WHERE \"Source\"='SS' AND \"EntityType\"='Training'",
                    x => x.GetString(0),
                    ct
                )
            ).ToHashSet(StringComparer.Ordinal);
            var byCode = (
                await QueryAsync("SELECT \"Id\",\"Code\" FROM \"Trainings\"", x => (x.GetInt32(0), x.GetString(1)), ct)
            )
                .GroupBy(x => x.Item2, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.Select(y => (int?)y.Item1).ToList(), StringComparer.Ordinal);
            var categories = (
                await QueryAsync(
                    "SELECT \"Id\",\"Name\" FROM \"TrainingCategories\"",
                    x => (x.GetInt32(0), x.GetString(1)),
                    ct
                )
            )
                .GroupBy(x => x.Item2, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.Select(y => (int?)y.Item1).ToList(), StringComparer.Ordinal);
            var maxOrder = (
                await QueryAsync(
                    "SELECT max(\"Order\") FROM \"Trainings\"",
                    x => x.IsDBNull(0) ? (int?)null : x.GetInt32(0),
                    ct
                )
            ).Single();
            List<int>? profiles = null;
            var created = new List<NewCourse>();
            var newCategories = new List<string>();
            var links =
                new List<(SsRow Row, NewCourse? Created, int? Existing, string Hash, string Outcome, string? Reason)>();
            var events = new List<Action<IReadOnlyDictionary<NewCourse, int>>>();
            foreach (var row in SsTrainingRules.Courses(lookup))
            {
                var outcomeIndex = events.Count;
                SsTrainingRules.CourseMapping? mapping = null;
                string? code = null;
                try
                {
                    mapping = SsTrainingRules.MapCourse(row, courseCodeCounts);
                    if (linked.Contains(row.Id))
                    {
                        events.Add(_ => ledger.Outcome("Training", row.Id, "Migrated", "ExistingCrosswalk"));
                        continue;
                    }
                    code = mapping.Code;
                    var shortened = code.Length > SsTrainingRules.CourseCodeMaxLength;
                    if (shortened)
                    {
                        var reserved = byCode.Keys.ToHashSet(StringComparer.Ordinal);
                        reserved.UnionWith(sourceCodes);
                        code = SsTrainingRules.AllocateCourseCode(code, reserved);
                    }
                    var targets = byCode.GetValueOrDefault(code) ?? [];
                    if (targets.Count > 1)
                        throw new SsReject("AmbiguousCourse");
                    NewCourse? course = null;
                    if (targets.Count == 0)
                    {
                        var category = SsTrainingRules.ApprovedCategory(row);
                        int? categoryId = null;
                        string? newCategory = null;
                        if (category is not null)
                        {
                            var found = categories.GetValueOrDefault(category) ?? [];
                            if (found.Count > 1)
                                throw new SsReject("AmbiguousTrainingCategory");
                            if (found.Count == 1)
                                categoryId = found[0];
                            if (found.Count == 0 || found[0] is null)
                                newCategory = category;
                        }
                        if (maxOrder == int.MaxValue)
                            throw new SsReject("OrderOverflow");
                        var order = maxOrder is null ? 0 : maxOrder.Value + 1;
                        int? profile = null;
                        if (SsTrainingRules.CarbineCourses.Contains(code))
                        {
                            if (!row.Bool("Mandatory"))
                                throw new SsReject("CarbineCourseNotMandatory");
                            profiles ??= await ActiveCarbineProfilesAsync(ct);
                            if (profiles.Count != 1)
                                throw new SsReject("MissingActiveCarbineProfile");
                            profile = profiles[0];
                        }
                        course = new(
                            row,
                            code,
                            mapping,
                            SsTrainingRules.Rotating(row, mapping, repeats),
                            newCategory,
                            categoryId,
                            order,
                            profile
                        );
                        // Commit tentative state only after every check passed (PerRow savepoint semantics).
                        if (newCategory is not null && !categories.ContainsKey(newCategory))
                        {
                            categories[newCategory] = [null];
                            newCategories.Add(newCategory);
                        }
                        byCode[code] = [null];
                        maxOrder = order;
                        created.Add(course);
                    }
                    var (outcome, reason) = SsTrainingRules.CourseLinkOutcome(shortened);
                    links.Add((row, course, course is null ? targets[0] : null, mapping.Hash, outcome, reason));
                    var linkIndex = links.Count - 1;
                    events.Add(ids =>
                    {
                        ledger.Outcome("Training", row.Id, outcome, reason);
                        if (links[linkIndex].Created is { } c)
                            ledger.Insert($"Training:{ids[c]}");
                    });
                }
                catch (SsReject reject)
                {
                    events.Add(_ => ledger.Outcome("Training", row.Id, "Rejected", reject.Message));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    events.Add(_ =>
                        ledger.Outcome("Training", row.Id, "Failed", $"TargetWriteFailed:{ex.GetType().Name}")
                    );
                }
            }

            // Persist: categories, courses and carbine requirements with pre-allocated identities.
            var categoryIds = (
                await SsBulkStaging.AllocateIdsAsync(connection, "TrainingCategories", newCategories.Count, ct)
            )
                .Select(x => (int)x)
                .ToList();
            var categoryByName = newCategories
                .Select((name, index) => (name, id: categoryIds[index]))
                .ToDictionary(x => x.name, x => x.id, StringComparer.Ordinal);
            var trainingIds = (await SsBulkStaging.AllocateIdsAsync(connection, "Trainings", created.Count, ct))
                .Select(x => (int)x)
                .ToList();
            var ids = created
                .Select((course, index) => (course, id: trainingIds[index]))
                .ToDictionary(x => x.course, x => x.id);
            var requirements = created.Where(x => x.Profile is not null).ToList();
            var requirementIds = (
                await SsBulkStaging.AllocateIdsAsync(
                    connection,
                    "TrainingMandatoryTrainingProfiles",
                    requirements.Count,
                    ct
                )
            )
                .Select(x => (int)x)
                .ToList();
            await SsBulkStaging.StageAsync(
                connection,
                "ss_bulk_categories",
                [
                    new SsBulkColumn<KeyValuePair<string, int>>("id", "integer", NpgsqlDbType.Integer, x => x.Value),
                    new("name", "text", NpgsqlDbType.Text, x => x.Key),
                ],
                categoryByName,
                ct
            );
            await ExecuteAsync(
                """
                INSERT INTO "TrainingCategories" ("Id","Name","Description","CreatedOn","CreatedById")
                SELECT id, name, NULL, @now, @actor FROM ss_bulk_categories ORDER BY id
                """,
                ct,
                new NpgsqlParameter("now", now),
                new NpgsqlParameter("actor", NpgsqlDbType.Uuid) { Value = (object?)actor ?? DBNull.Value }
            );
            await SsBulkStaging.StageAsync(
                connection,
                "ss_bulk_trainings",
                [
                    new SsBulkColumn<NewCourse>("id", "integer", NpgsqlDbType.Integer, x => ids[x]),
                    new("code", "text", NpgsqlDbType.Text, x => x.Code),
                    new("description", "text", NpgsqlDbType.Text, x => x.Mapping.Description),
                    new("effective", "timestamptz", NpgsqlDbType.TimestampTz, x => x.Row.Date("EffectiveDate") ?? now),
                    new("expiry", "timestamptz", NpgsqlDbType.TimestampTz, x => x.Row.Date("ExpiryDate")),
                    new("mandatory", "boolean", NpgsqlDbType.Boolean, x => x.Row.Bool("Mandatory")),
                    new("rotating", "boolean", NpgsqlDbType.Boolean, x => x.Rotating),
                    new("validity", "integer", NpgsqlDbType.Integer, x => x.Mapping.Days),
                    new("notice", "integer", NpgsqlDbType.Integer, x => SsTrainingRules.AdvanceNoticeDays(x.Mapping)),
                    new(
                        "category",
                        "integer",
                        NpgsqlDbType.Integer,
                        x => x.CategoryName is null ? x.CategoryId : categoryByName[x.CategoryName]
                    ),
                    new("ord", "integer", NpgsqlDbType.Integer, x => x.Order),
                ],
                created,
                ct
            );
            await ExecuteAsync(
                """
                INSERT INTO "Trainings" ("Id","Code","Description","EffectiveDate","ExpiryDate","Mandatory","Rotating",
                  "ValidityDays","AdvanceNoticeDays","TrainingCategoryId","Order","CreatedOn","CreatedById")
                SELECT id, code, description, effective, expiry, mandatory, rotating, validity, notice, category, ord,
                  @now, @actor FROM ss_bulk_trainings ORDER BY id
                """,
                ct,
                new NpgsqlParameter("now", now),
                new NpgsqlParameter("actor", NpgsqlDbType.Uuid) { Value = (object?)actor ?? DBNull.Value }
            );
            await SsBulkStaging.StageAsync(
                connection,
                "ss_bulk_requirements",
                [
                    new SsBulkColumn<(NewCourse Course, int Id)>("id", "integer", NpgsqlDbType.Integer, x => x.Id),
                    new("training", "integer", NpgsqlDbType.Integer, x => ids[x.Course]),
                    new("profile", "integer", NpgsqlDbType.Integer, x => x.Course.Profile),
                ],
                requirements.Select((course, index) => (Course: course, Id: requirementIds[index])),
                ct
            );
            await ExecuteAsync(
                """
                INSERT INTO "TrainingMandatoryTrainingProfiles" ("Id","TrainingId","TrainingProfileTypeId","CreatedOn","CreatedById")
                SELECT id, training, profile, @now, @actor FROM ss_bulk_requirements ORDER BY id
                """,
                ct,
                new NpgsqlParameter("now", now),
                new NpgsqlParameter("actor", NpgsqlDbType.Uuid) { Value = (object?)actor ?? DBNull.Value }
            );

            var linkedIds = links.Select(x => x.Created is { } c ? ids[c] : x.Existing!.Value).ToList();
            var images = await SsBulkTargets.ImagesAsync<Training, int>(db, linkedIds, auditOptions, ct);
            await SsBulkCrosswalk.InsertLinksAsync(
                connection,
                run.Id,
                links
                    .Select(
                        (x, index) =>
                            new SsBulkLink(
                                "Training",
                                x.Row.Id,
                                "Training",
                                Key(linkedIds[index]),
                                SsWriteSession.ProposalHash("Training", x.Row, x.Hash),
                                images[Key(linkedIds[index])].Fingerprint,
                                x.Created is null ? null : run.Id,
                                x.Row.Date("UpdatedOn") ?? x.Row.Date("CreatedOn")
                            )
                    )
                    .ToList(),
                ct
            );
            await SsBulkCrosswalk.AppendMutationsAsync(
                connection,
                run.Id,
                links
                    .Select(
                        (x, index) =>
                            new SsBulkMutation(
                                null,
                                "Training",
                                x.Row.Id,
                                x.Created is null ? "Link" : "Insert",
                                images[Key(linkedIds[index])].Fingerprint,
                                images[Key(linkedIds[index])].Version
                            )
                    )
                    .ToList(),
                ct
            );
            var audit = new SsBulkAudit(db);
            audit.Added(
                nameof(TrainingCategory),
                "TrainingCategories",
                (await SsBulkTargets.ImagesAsync<TrainingCategory, int>(db, categoryIds, auditOptions, ct)).Values
            );
            audit.Added(nameof(Training), "Trainings", trainingIds.Select(x => images[Key(x)]));
            audit.Added(
                nameof(TrainingProfileRequirement),
                "TrainingMandatoryTrainingProfiles",
                (
                    await SsBulkTargets.ImagesAsync<TrainingProfileRequirement, int>(
                        db,
                        requirementIds,
                        auditOptions,
                        ct
                    )
                ).Values
            );
            await audit.FlushAsync(ct);
            foreach (var item in events)
                item(ids);
        }

        // ---------------------------------------------------------------- histories (chunks 1..n)

        /// <summary>
        /// Groups SheriffTraining rows by target user (union-find also joins an owned target's current user
        /// with its newly resolved user) and packs whole groups into chunks of about <paramref name="size"/> rows.
        /// </summary>
        public async Task<List<List<(int Order, SsRow Row)>>> PlanHistoryChunksAsync(int size, CancellationToken ct)
        {
            var ordered = SsTrainingRules
                .HistoryOrder(rows["SheriffTraining"])
                .Select((row, index) => (index, row))
                .ToList();
            var userKeys = (
                await QueryAsync(
                    "SELECT \"LegacyId\",\"TargetKey\" FROM \"DataMigrationRecords\" WHERE \"Source\"='SS' AND \"EntityType\"='User'",
                    x => (x.GetString(0), x.GetString(1)),
                    ct
                )
            ).ToDictionary(x => x.Item1, x => x.Item2, StringComparer.Ordinal);
            var owned = (
                await QueryAsync(
                    """
                    SELECT r."LegacyId", u."UserId"::text FROM "DataMigrationRecords" r
                    JOIN "UserTrainings" u ON r."TargetType"='UserTraining' AND u."Id"::text=r."TargetKey"
                    WHERE r."Source"='SS' AND r."EntityType"='SheriffTraining'
                    """,
                    x => (x.GetString(0), x.GetString(1)),
                    ct
                )
            ).ToDictionary(x => x.Item1, x => x.Item2, StringComparer.Ordinal);
            var parent = new Dictionary<string, string>(StringComparer.Ordinal);
            string Find(string node)
            {
                if (!parent.TryGetValue(node, out var next))
                    return parent[node] = node;
                if (next == node)
                    return node;
                return parent[node] = Find(next);
            }
            void Union(string a, string b)
            {
                var (x, y) = (Find(a), Find(b));
                if (x != y)
                    parent[x] = y;
            }
            var nodes = new List<string>(ordered.Count);
            foreach (var (_, row) in ordered)
            {
                var source = row.Guid("SheriffId")?.ToString();
                var node =
                    source is null ? $"row:{row.Id}"
                    : source == SystemUserKey ? SystemUserKey
                    : userKeys.GetValueOrDefault(source) ?? $"source:{source}";
                Find(node);
                if (owned.TryGetValue(row.Id, out var ownedUser))
                    Union(node, ownedUser);
                nodes.Add(node);
            }
            var groups = ordered
                .Select((item, index) => (item.index, item.row, root: Find(nodes[index])))
                .GroupBy(x => x.root, StringComparer.Ordinal)
                .OrderBy(x => x.Min(y => y.index))
                .ToList();
            var chunks = new List<List<(int, SsRow)>>();
            var current = new List<(int, SsRow)>();
            foreach (var group in groups)
            {
                current.AddRange(group.Select(x => (x.index, x.row)));
                if (current.Count >= size)
                {
                    chunks.Add([.. current.OrderBy(x => x.Item1)]);
                    current = [];
                }
            }
            if (current.Count > 0)
                chunks.Add([.. current.OrderBy(x => x.Item1)]);
            return chunks;
        }

        public async Task WriteHistoriesAsync(
            NpgsqlConnection connection,
            List<(int Order, SsRow Row)> chunk,
            CancellationToken ct
        )
        {
            await ExecuteAsync("LOCK TABLE \"UserTrainings\" IN SHARE ROW EXCLUSIVE MODE", ct);
            var sourceUsers = chunk
                .Select(x => x.Row.Guid("SheriffId")?.ToString())
                .OfType<string>()
                .Distinct()
                .ToArray();
            var userRecords = await RecordsAsync("User", sourceUsers, ct);
            var systemUserExists = (
                await QueryAsync(
                    "SELECT EXISTS (SELECT 1 FROM \"Users\" WHERE \"Id\"=@id)",
                    x => x.GetBoolean(0),
                    ct,
                    new NpgsqlParameter("id", User.SystemUser)
                )
            ).Single();
            var records = (
                await RecordsAsync("SheriffTraining", chunk.Select(x => x.Row.Id).ToArray(), ct)
            ).ToDictionary(x => x.LegacyId, StringComparer.Ordinal);
            var courseKeys = (
                await QueryAsync(
                    "SELECT \"LegacyId\",\"TargetKey\" FROM \"DataMigrationRecords\" WHERE \"Source\"='SS' AND \"EntityType\"='Training'",
                    x => (x.GetString(0), x.GetString(1)),
                    ct
                )
            ).ToDictionary(x => x.Item1, x => x.Item2, StringComparer.Ordinal);
            var courseCodes = (
                await QueryAsync("SELECT \"Id\",\"Code\" FROM \"Trainings\"", x => (x.GetInt32(0), x.GetString(1)), ct)
            ).ToDictionary(x => x.Item1, x => x.Item2);
            var ownedIds = records
                .Values.Where(x => x.TargetType == "UserTraining")
                .Select(x => int.TryParse(x.TargetKey, CultureInfo.InvariantCulture, out var id) ? id : (int?)null)
                .OfType<int>()
                .ToArray();
            var byId = new Dictionary<int, Ut>();
            foreach (
                var ut in await QueryAsync(
                    """
                    SELECT "Id","UserId","TrainingId","Version","EndingOn","AwardedOn" FROM "UserTrainings"
                    WHERE "Id" = ANY(@ids) ORDER BY "Id" FOR UPDATE
                    """,
                    ReadUt,
                    ct,
                    new NpgsqlParameter("ids", ownedIds)
                )
            )
                byId[ut.Id] = ut;
            var targetUsers = new HashSet<Guid>(byId.Values.Select(x => x.UserId));
            foreach (var record in userRecords)
                if (Guid.TryParse(record.TargetKey, out var id))
                    targetUsers.Add(id);
            if (systemUserExists)
                targetUsers.Add(User.SystemUser);
            foreach (
                var ut in await QueryAsync(
                    """
                    SELECT "Id","UserId","TrainingId","Version","EndingOn","AwardedOn" FROM "UserTrainings"
                    WHERE "UserId" = ANY(@users)
                    """,
                    ReadUt,
                    ct,
                    new NpgsqlParameter("users", targetUsers.ToArray())
                )
            )
                byId.TryAdd(ut.Id, ut);
            var pairs = byId.Values.GroupBy(x => (x.UserId, x.TrainingId)).ToDictionary(x => x.Key, x => x.ToList());
            var userState = (
                await QueryAsync(
                    "SELECT \"Id\",\"TrainingProfileId\",xmin::text FROM \"Users\" WHERE \"Id\" = ANY(@users)",
                    x =>
                        (
                            Id: x.GetGuid(0),
                            Profile: x.IsDBNull(1) ? (int?)null : x.GetInt32(1),
                            Version: x.GetString(2)
                        ),
                    ct,
                    new NpgsqlParameter("users", targetUsers.ToArray())
                )
            ).ToDictionary(x => x.Id, x => (x.Profile, x.Version));
            var latest = (
                await QueryAsync(
                    """
                    SELECT DISTINCT ON ("DataMigrationRecordId") "DataMigrationRecordId","Version"
                    FROM "DataMigrationMutations" WHERE "DataMigrationRecordId" = ANY(@ids)
                    ORDER BY "DataMigrationRecordId","Id" DESC
                    """,
                    x => (x.GetInt64(0), x.IsDBNull(1) ? null : x.GetString(1)),
                    ct,
                    new NpgsqlParameter(
                        "ids",
                        records.Values.Select(x => x.Id).Concat(userRecords.Select(x => x.Id)).ToArray()
                    )
                )
            ).ToDictionary(x => x.Item1, x => x.Item2);
            var userRecordsBySource = userRecords.GroupBy(x => x.LegacyId).ToDictionary(x => x.Key, x => x.ToList());

            string ResolveUser(string sourceUser)
            {
                if (sourceUser == SystemUserKey)
                    return systemUserExists ? sourceUser : throw new SsReject("MissingUnifiedSystemUser");
                var found = userRecordsBySource.GetValueOrDefault(sourceUser);
                return found is { Count: 1 } ? found[0].TargetKey : throw new SsReject("MissingParent");
            }
            int ResolveCourse(SsRow row)
            {
                var source = row.Int("TrainingTypeId") ?? throw new SsReject("MissingTraining");
                return int.Parse(
                    courseKeys.GetValueOrDefault(source.ToString(CultureInfo.InvariantCulture))
                        ?? throw new SsReject("MissingParent"),
                    CultureInfo.InvariantCulture
                );
            }

            // Pre-images needed for drift checks: owned targets whose mapped source changed, and users that
            // a carbine history may compose. Rows that cannot hash/resolve simply reject later.
            var driftCandidates = new HashSet<int>();
            var composeCandidates = new HashSet<Guid>();
            foreach (var (_, row) in chunk)
            {
                try
                {
                    var sourceUser = row.Guid("SheriffId")?.ToString();
                    if (sourceUser is null || !users.ContainsKey(sourceUser))
                        continue;
                    if (records.TryGetValue(row.Id, out var record))
                    {
                        var dates = SsWriteSession.Dates(row);
                        var hash = SsTrainingRules.HistoryHash(row, sourceUser, dates);
                        if (
                            record.PayloadHash != SsWriteSession.ProposalHash("SheriffTraining", row, hash)
                            && int.TryParse(record.TargetKey, CultureInfo.InvariantCulture, out var id)
                        )
                            driftCandidates.Add(id);
                        continue;
                    }
                    var course = ResolveCourse(row);
                    // Version placement may renumber existing rows of the group; prefetch their pre-images.
                    try
                    {
                        var target = Guid.Parse(ResolveUser(sourceUser));
                        if (
                            pairs.TryGetValue((target, course), out var group)
                            && SsTrainingRules.MayHaveNewerHistory(
                                group.Select(x => x.EndingOn),
                                SsWriteSession.Dates(row).End
                            )
                        )
                            foreach (var existing in group)
                                driftCandidates.Add(existing.Id);
                    }
                    catch (SsReject) { }
                    if (
                        courseCodes.TryGetValue(course, out var code)
                        && SsTrainingRules.IsCarbineHistory(code, row.Int("TrainingTypeId"), shortenedCarbine)
                    )
                        foreach (var record2 in userRecordsBySource.GetValueOrDefault(sourceUser) ?? [])
                            if (
                                Guid.TryParse(record2.TargetKey, out var userId)
                                && userState.TryGetValue(userId, out var state)
                                && state.Profile is null
                            )
                                composeCandidates.Add(userId);
                }
                catch (SsReject) { }
            }
            await SsBulkTargets.VersionsAsync(
                db,
                db.Model.FindEntityType(typeof(User))!,
                composeCandidates.Cast<object>().ToArray(),
                ct,
                forUpdate: true
            );
            var ownedBefore = await SsBulkTargets.ImagesAsync<UserTraining, int>(db, driftCandidates, auditOptions, ct);
            var usersBefore = await SsBulkTargets.ImagesAsync<User, Guid>(db, composeCandidates, auditOptions, ct);
            // Crosswalk evidence for rows the version placement may renumber (same checks as PerRow).
            var groupRecords = (
                await QueryAsync(
                    """
                    SELECT "Id","LegacyId","TargetType","TargetKey","PayloadHash","AfterHash","CreatedByRunId","SourceUpdatedAtUtc"
                    FROM "DataMigrationRecords"
                    WHERE "Source"='SS' AND "EntityType"='SheriffTraining' AND "TargetType"='UserTraining'
                      AND "TargetKey" = ANY(@keys)
                    ORDER BY "Id"
                    """,
                    ReadRec,
                    ct,
                    new NpgsqlParameter("keys", driftCandidates.Select(Key).ToArray())
                )
            ).ToLookup(x => x.TargetKey, StringComparer.Ordinal);
            var groupProofs = (
                await QueryAsync(
                    """
                    SELECT DISTINCT ON ("DataMigrationRecordId") "DataMigrationRecordId","Version"
                    FROM "DataMigrationMutations" WHERE "DataMigrationRecordId" = ANY(@ids)
                    ORDER BY "DataMigrationRecordId","Id" DESC
                    """,
                    x => (x.GetInt64(0), x.IsDBNull(1) ? null : x.GetString(1)),
                    ct,
                    new NpgsqlParameter("ids", groupRecords.SelectMany(x => x).Select(x => x.Id).ToArray())
                )
            ).ToDictionary(x => x.Item1, x => x.Item2);
            var recordsByTarget = records
                .Values.Where(x => x.TargetType == "UserTraining")
                .GroupBy(x => x.TargetKey, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.OrderBy(y => y.Id).ToList(), StringComparer.Ordinal);
            // Compose decisions must use the state read under the row lock, not the earlier unlocked read.
            foreach (
                var locked in await QueryAsync(
                    "SELECT \"Id\",\"TrainingProfileId\",xmin::text FROM \"Users\" WHERE \"Id\" = ANY(@users)",
                    x =>
                        (
                            Id: x.GetGuid(0),
                            Profile: x.IsDBNull(1) ? (int?)null : x.GetInt32(1),
                            Version: x.GetString(2)
                        ),
                    ct,
                    new NpgsqlParameter("users", composeCandidates.ToArray())
                )
            )
                userState[locked.Id] = (locked.Profile, locked.Version);
            var profiles = await ActiveCarbineProfilesAsync(ct);

            var writes = new List<HistoryWrite>();
            var composes = new List<(Guid User, int Profile)>();
            var stamps = new List<(Rec Record, DateTimeOffset? Changed)>();
            var intents = new List<Intent>();
            var nextTemp = -1;
            var touched = new HashSet<int>();
            // Version placement: new rows' source IDs, untouched owned rows moved up (renumbered), and rows
            // already written in this chunk whose version then moved (they must park like a rebind).
            var newLegacy = new Dictionary<int, string>();
            var renumbered = new Dictionary<int, Ut>();
            var shiftedWrites = new HashSet<int>();

            string? Legacy(Ut x)
            {
                if (newLegacy.TryGetValue(x.Id, out var legacy))
                    return legacy;
                var key = Key(x.Id);
                var linked = groupRecords[key].ToList();
                if (linked.Count == 0)
                    linked = recordsByTarget.GetValueOrDefault(key) ?? [];
                return (linked.FirstOrDefault(y => y.CreatedByRunId is not null) ?? linked.FirstOrDefault())?.LegacyId;
            }
            string? Block(int id)
            {
                // Written by this run in this chunk: its evidence is refreshed on persist (PerRow re-reads it).
                if (id < 0 || touched.Contains(id) || renumbered.ContainsKey(id))
                    return null;
                var key = Key(id);
                var owner = groupRecords[key].FirstOrDefault(x => x.CreatedByRunId is not null);
                if (owner is null)
                    return SsTrainingRules.UnownedNewerHistoryReason;
                if (groupProofs.GetValueOrDefault(owner.Id) is null)
                    return "MissingOriginalTargetVersionProof";
                if (!ownedBefore.TryGetValue(key, out var image))
                    throw new InvalidOperationException(
                        "SS bulk training version placement target was not prefetched."
                    );
                return image.Fingerprint == owner.AfterHash ? null : "OwnedTargetDrift";
            }

            foreach (var (_, row) in chunk)
                Simulate(
                    "SheriffTraining",
                    row,
                    () =>
                    {
                        // Training has no history date limit (same as SsTrainingWriter).
                        var sourceUser = row.Guid("SheriffId")?.ToString() ?? throw new SsReject("MissingUser");
                        if (!users.TryGetValue(sourceUser, out var sheriff))
                            throw new SsReject("MissingSourceUser");
                        records.TryGetValue(row.Id, out var record);
                        var excused = SsTrainingRules.ExcludedExcused(sheriff, includeExcused);
                        if (row.Date("ExpiryDate") is not null || excused)
                        {
                            ledger.Outcome(
                                "SheriffTraining",
                                row.Id,
                                "Excluded",
                                SsTrainingRules.ExcludedHistoryReason(record is not null, excused)
                            );
                            return;
                        }
                        var dates = SsWriteSession.Dates(row);
                        SsWriteSession.Timezone(row, false);
                        var hash = SsTrainingRules.HistoryHash(row, sourceUser, dates);
                        var mapped = SsWriteSession.ProposalHash("SheriffTraining", row, hash);
                        var changed = row.Date("UpdatedOn") ?? row.Date("CreatedOn");
                        if (record is not null)
                        {
                            if (record.PayloadHash == mapped)
                            {
                                if (record.SourceUpdatedAtUtc == changed)
                                {
                                    ledger.Outcome("SheriffTraining", row.Id, "Migrated", "ExistingCrosswalk");
                                    return;
                                }
                                stamps.Add((record, changed));
                                ledger.Outcome("SheriffTraining", row.Id, "Migrated", "UnchangedMappedSource");
                                return;
                            }
                            if (record.CreatedByRunId is null)
                                throw new SsReject("LinkedOnlyTargetCannotUpdate");
                            if (latest.GetValueOrDefault(record.Id) is null)
                                throw new SsReject("MissingOriginalTargetVersionProof");
                            if (
                                record.TargetType != "UserTraining"
                                || !int.TryParse(record.TargetKey, CultureInfo.InvariantCulture, out var ownedId)
                                || !byId.TryGetValue(ownedId, out var owned)
                            )
                                throw new SsReject("MissingCrosswalkTarget");
                            // A target already rewritten in this chunk (alias rows) would be compared against a
                            // stale pre-image; PerRow re-reads it. Abort the chunk rather than diverge.
                            if (touched.Contains(ownedId))
                                throw new InvalidOperationException(
                                    "SS bulk owned target was written twice in one chunk."
                                );
                            // Same as PerRow (ReplaySkippingDriftAsync): a target edited in Unified since the
                            // job last wrote it is skipped and left unchanged, never overwritten from SS.
                            if (ownedBefore[record.TargetKey].Fingerprint != record.AfterHash)
                            {
                                ledger.Outcome("SheriffTraining", row.Id, "Excluded", "OwnedTargetDrift");
                                return;
                            }
                            var resolvedUser = Guid.Parse(ResolveUser(sourceUser));
                            var resolvedCourse = ResolveCourse(row);
                            var rebind = owned.UserId != resolvedUser || owned.TrainingId != resolvedCourse;
                            var version = owned.Version;
                            string code;
                            if (rebind)
                            {
                                version = SsTrainingRules.NextVersion(
                                    pairs
                                        .GetValueOrDefault((resolvedUser, resolvedCourse))
                                        ?.Where(x => x.Id != owned.Id)
                                        .Select(x => (int?)x.Version)
                                        .Max()
                                );
                                code =
                                    courseCodes.GetValueOrDefault(resolvedCourse)
                                    ?? throw new RowFailure(nameof(InvalidOperationException));
                            }
                            else
                                code = courseCodes[owned.TrainingId];
                            var notes = SsTrainingRules.MappedNotes(row, code);
                            if (owned.UserId != resolvedUser && !userState.ContainsKey(resolvedUser))
                                throw new RowFailure(nameof(DbUpdateException));
                            EnsureNotesFit(notes);
                            touched.Add(ownedId);
                            if (rebind)
                            {
                                pairs[(owned.UserId, owned.TrainingId)].Remove(owned);
                                owned.UserId = resolvedUser;
                                owned.TrainingId = resolvedCourse;
                                owned.Version = version;
                                Pair(pairs, resolvedUser, resolvedCourse).Add(owned);
                            }
                            owned.EndingOn = dates.End;
                            owned.AwardedOn = dates.Start;
                            writes.Add(
                                new(
                                    owned,
                                    row,
                                    mapped,
                                    dates.Start,
                                    dates.End,
                                    row.Date("TrainingCertificationExpiry"),
                                    notes,
                                    SsTrainingRules.NoticeState(row),
                                    record,
                                    rebind
                                )
                            );
                            intents.Add(new("Update", writes.Count - 1));
                            ledger.Update($"UserTraining:{record.TargetKey}");
                            ledger.Outcome("SheriffTraining", row.Id, "Migrated", "OwnedMappedUpdate");
                            return;
                        }
                        var userId = Guid.Parse(ResolveUser(sourceUser));
                        var courseId = ResolveCourse(row);
                        var courseCode =
                            courseCodes.GetValueOrDefault(courseId)
                            ?? throw new RowFailure(nameof(InvalidOperationException));
                        var mappedNotes = SsTrainingRules.MappedNotes(row, courseCode);
                        var histories = pairs.GetValueOrDefault((userId, courseId)) ?? [];
                        // Same first-run order as PerRow: an older backfilled history never takes the top version.
                        var placement = SsTrainingRules.PlaceHistory(
                            [
                                .. histories.Select(x => new SsTrainingRules.HistorySlot(
                                    x.Id,
                                    x.Version,
                                    x.AwardedOn,
                                    x.EndingOn,
                                    Legacy(x)
                                )),
                            ],
                            row.Id,
                            dates.Start,
                            dates.End,
                            x => Block(x.Id)
                        );
                        if (placement.Skip is { } skip)
                        {
                            ledger.Outcome("SheriffTraining", row.Id, "Excluded", skip);
                            return;
                        }
                        int? composeProfile = null;
                        if (SsTrainingRules.IsCarbineHistory(courseCode, row.Int("TrainingTypeId"), shortenedCarbine))
                        {
                            if (!userState.TryGetValue(userId, out var state))
                                throw new RowFailure(nameof(InvalidOperationException));
                            if (state.Profile is null)
                            {
                                if (profiles.Count != 1)
                                    throw new SsReject("MissingActiveCarbineProfile");
                                var owners = userRecordsBySource.GetValueOrDefault(sourceUser) ?? [];
                                if (owners.Count != 1 || owners.Any(x => x.CreatedByRunId is null))
                                    throw new SsReject("CompositionRequiresOwnedTargets");
                                foreach (var owner in owners)
                                {
                                    var target = Guid.Parse(owner.TargetKey);
                                    if (!userState.TryGetValue(target, out var targetState))
                                        throw new SsReject("MissingCrosswalkTarget");
                                    var prior = latest.GetValueOrDefault(owner.Id);
                                    if (
                                        prior is null
                                        || targetState.Version != prior
                                        || !usersBefore.TryGetValue(owner.TargetKey, out var image)
                                        || image.Version != prior
                                        || image.Fingerprint != owner.AfterHash
                                    )
                                        throw new SsReject("OwnedTargetDrift");
                                }
                                composeProfile = profiles[0];
                            }
                        }
                        else if (!userState.ContainsKey(userId))
                            throw new RowFailure(nameof(DbUpdateException));
                        EnsureNotesFit(mappedNotes);
                        var older = histories.Any(x => x.EndingOn > dates.End);
                        var byVersionId = histories.ToDictionary(x => x.Id);
                        foreach (var id in placement.Shift)
                        {
                            var moved = byVersionId[id];
                            moved.Version = SsTrainingRules.NextVersion(moved.Version);
                            if (id < 0)
                                continue; // Inserted later in this chunk at its final version.
                            if (touched.Contains(id))
                                shiftedWrites.Add(id);
                            else
                                renumbered[id] = moved;
                            intents.Add(new("Renumber", id));
                            ledger.Update($"UserTraining:{Key(id)}");
                        }
                        if (composeProfile is { } profile)
                        {
                            userState[userId] = (profile, userState[userId].Version);
                            composes.Add((userId, profile));
                            intents.Add(new("Compose", composes.Count - 1));
                        }
                        var created = new Ut
                        {
                            Id = nextTemp--,
                            UserId = userId,
                            TrainingId = courseId,
                            Version = placement.Version,
                            EndingOn = dates.End,
                            AwardedOn = dates.Start,
                        };
                        newLegacy[created.Id] = row.Id;
                        Pair(pairs, userId, courseId).Add(created);
                        writes.Add(
                            new(
                                created,
                                row,
                                mapped,
                                dates.Start,
                                dates.End,
                                row.Date("TrainingCertificationExpiry"),
                                mappedNotes,
                                SsTrainingRules.NoticeState(row),
                                null,
                                false
                            )
                        );
                        intents.Add(new("Insert", writes.Count - 1));
                        ledger.Outcome(
                            "SheriffTraining",
                            row.Id,
                            "MigratedWithDifference",
                            SsTrainingRules.HistoryLinkReason(row, older)
                        );
                    }
                );

            await PersistHistoriesAsync(
                connection,
                writes,
                composes,
                stamps,
                intents,
                ownedBefore,
                usersBefore,
                renumbered,
                shiftedWrites,
                groupRecords,
                recordsByTarget,
                ct
            );
        }

        private async Task PersistHistoriesAsync(
            NpgsqlConnection connection,
            List<HistoryWrite> writes,
            List<(Guid User, int Profile)> composes,
            List<(Rec Record, DateTimeOffset? Changed)> stamps,
            List<Intent> intents,
            Dictionary<string, SsBulkTargetImage> ownedBefore,
            Dictionary<string, SsBulkTargetImage> usersBefore,
            Dictionary<int, Ut> renumbered,
            HashSet<int> shiftedWrites,
            ILookup<string, Rec> groupRecords,
            Dictionary<string, List<Rec>> recordsByTarget,
            CancellationToken ct
        )
        {
            var inserts = writes.Where(x => x.Record is null).ToList();
            var updates = writes.Where(x => x.Record is not null).ToList();
            var newIds = await SsBulkStaging.AllocateIdsAsync(connection, "UserTrainings", inserts.Count, ct);
            for (var i = 0; i < inserts.Count; i++)
                inserts[i].State.Id = (int)newIds[i];
            if (writes.Any(x => NotesTooLong(x.Notes)))
                throw new InvalidOperationException("SS bulk training notes exceed the target column.");
            var actorParameter = new Func<NpgsqlParameter>(() =>
                new NpgsqlParameter("actor", NpgsqlDbType.Uuid) { Value = (object?)actor ?? DBNull.Value }
            );
            var historyColumns = new SsBulkColumn<HistoryWrite>[]
            {
                new("id", "integer", NpgsqlDbType.Integer, x => x.State.Id),
                new("user_id", "uuid", NpgsqlDbType.Uuid, x => x.State.UserId),
                new("training_id", "integer", NpgsqlDbType.Integer, x => x.State.TrainingId),
                new("version", "integer", NpgsqlDbType.Integer, x => x.State.Version),
                new("awarded", "timestamptz", NpgsqlDbType.TimestampTz, x => x.AwardedOn),
                new("ending", "timestamptz", NpgsqlDbType.TimestampTz, x => x.EndingOn),
                new("expiry", "timestamptz", NpgsqlDbType.TimestampTz, x => x.ExpiryDate),
                new("notes", "text", NpgsqlDbType.Text, x => x.Notes),
                new("notice", "text", NpgsqlDbType.Text, x => x.NoticeState),
                new(
                    "rebind",
                    "boolean",
                    NpgsqlDbType.Boolean,
                    x => x.VersionChanged || shiftedWrites.Contains(x.State.Id)
                ),
            };
            var renumberColumns = new SsBulkColumn<Ut>[]
            {
                new("id", "integer", NpgsqlDbType.Integer, x => x.Id),
                new("version", "integer", NpgsqlDbType.Integer, x => x.Version),
            };
            if (updates.Count > 0)
                await SsBulkStaging.StageAsync(connection, "ss_bulk_history_updates", historyColumns, updates, ct);
            if (renumbered.Count > 0)
                await SsBulkStaging.StageAsync(
                    connection,
                    "ss_bulk_history_renumbers",
                    renumberColumns,
                    renumbered.Values,
                    ct
                );
            // Two-phase version move: park every row whose version changes (rebinds, renumbers) on unique
            // negative versions first so moves never collide transiently on the unique version index.
            if (updates.Count > 0)
                await ExecuteAsync(
                    """
                    UPDATE "UserTrainings" t SET "Version" = -t."Id"
                    FROM ss_bulk_history_updates s WHERE t."Id"=s.id AND s.rebind
                    """,
                    ct
                );
            if (renumbered.Count > 0)
            {
                await ExecuteAsync(
                    """
                    UPDATE "UserTrainings" t SET "Version" = -t."Id"
                    FROM ss_bulk_history_renumbers s WHERE t."Id"=s.id
                    """,
                    ct
                );
                // Only the version (and the migration update stamp) changes; dates and notes stay as they are.
                var moved = await ExecuteAsync(
                    """
                    UPDATE "UserTrainings" t SET "Version"=s.version, "UpdatedOn"=@now,
                      "UpdatedById"=CASE WHEN @stamp THEN @actor ELSE t."UpdatedById" END
                    FROM ss_bulk_history_renumbers s WHERE t."Id"=s.id
                    """,
                    ct,
                    new NpgsqlParameter("now", now),
                    new NpgsqlParameter("stamp", actor is not null),
                    actorParameter()
                );
                if (moved != renumbered.Count)
                    throw new InvalidOperationException("SS bulk renumbered training target disappeared.");
            }
            if (updates.Count > 0)
            {
                var updated = await ExecuteAsync(
                    """
                    UPDATE "UserTrainings" t SET
                      "UserId"=s.user_id, "TrainingId"=s.training_id, "Version"=s.version,
                      "AwardedOn"=s.awarded, "EndingOn"=s.ending, "ExpiryDate"=s.expiry,
                      "Notes"=s.notes, "NoticeState"=s.notice, "UpdatedOn"=@now,
                      "UpdatedById"=CASE WHEN @stamp THEN @actor ELSE t."UpdatedById" END
                    FROM ss_bulk_history_updates s WHERE t."Id"=s.id
                    """,
                    ct,
                    new NpgsqlParameter("now", now),
                    new NpgsqlParameter("stamp", actor is not null),
                    actorParameter()
                );
                if (updated != updates.Count)
                    throw new InvalidOperationException("SS bulk owned training target disappeared.");
            }
            if (inserts.Count > 0)
            {
                await SsBulkStaging.StageAsync(connection, "ss_bulk_history_inserts", historyColumns, inserts, ct);
                await ExecuteAsync(
                    """
                    INSERT INTO "UserTrainings" ("Id","UserId","TrainingId","Version","AwardedOn","EndingOn","AllDay",
                      "ExpiryDate","NoticeState","Notes","CreatedOn","CreatedById")
                    SELECT id, user_id, training_id, version, awarded, ending, false, expiry, notice, notes, @now, @actor
                    FROM ss_bulk_history_inserts ORDER BY id
                    """,
                    ct,
                    new NpgsqlParameter("now", now),
                    actorParameter()
                );
            }
            if (composes.Count > 0)
            {
                await SsBulkStaging.StageAsync(
                    connection,
                    "ss_bulk_composes",
                    [
                        new SsBulkColumn<(Guid User, int Profile)>("user_id", "uuid", NpgsqlDbType.Uuid, x => x.User),
                        new("profile", "integer", NpgsqlDbType.Integer, x => x.Profile),
                    ],
                    composes,
                    ct
                );
                var composed = await ExecuteAsync(
                    """
                    UPDATE "Users" u SET "TrainingProfileId"=s.profile, "UpdatedOn"=@now,
                      "UpdatedById"=CASE WHEN @stamp THEN @actor ELSE u."UpdatedById" END
                    FROM ss_bulk_composes s WHERE u."Id"=s.user_id AND u."TrainingProfileId" IS NULL
                    """,
                    ct,
                    new NpgsqlParameter("now", now),
                    new NpgsqlParameter("stamp", actor is not null),
                    actorParameter()
                );
                if (composed != composes.Count)
                    throw new InvalidOperationException("SS bulk compose target changed concurrently.");
            }

            var historyAfter = await SsBulkTargets.ImagesAsync<UserTraining, int>(
                db,
                writes.Select(x => x.State.Id).Concat(renumbered.Keys).Distinct().ToList(),
                auditOptions,
                ct
            );
            var usersAfter = await SsBulkTargets.ImagesAsync<User, Guid>(
                db,
                composes.Select(x => x.User).ToList(),
                auditOptions,
                ct
            );
            foreach (var compose in composes)
            {
                var key = compose.User.ToString("D");
                if (usersAfter[key].Fingerprint != usersBefore[key].Fingerprint)
                    throw new InvalidOperationException("SS bulk compose changed MyTeam-mapped user fields.");
            }
            var renumberEvents = intents
                .Where(x => x.Kind == "Renumber")
                .GroupBy(x => x.Index)
                .ToDictionary(x => x.Key, x => x.Count());
            var updatedKeys = updates
                .Select(x => x.Record!.TargetKey)
                .Concat(renumberEvents.Keys.Select(Key))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var composedKeys = composes.Select(x => x.User.ToString("D")).ToArray();
            var aliases = await QueryAsync(
                """
                SELECT "Id","LegacyId","TargetType","TargetKey","PayloadHash","AfterHash","CreatedByRunId","SourceUpdatedAtUtc"
                FROM "DataMigrationRecords"
                WHERE "Source"='SS' AND (("TargetType"='UserTraining' AND "TargetKey" = ANY(@updated))
                   OR ("TargetType"='User' AND "TargetKey" = ANY(@composed)))
                ORDER BY "Id"
                """,
                ReadRec,
                ct,
                new NpgsqlParameter("updated", updatedKeys),
                new NpgsqlParameter("composed", composedKeys)
            );

            await SsBulkCrosswalk.InsertLinksAsync(
                connection,
                run.Id,
                inserts
                    .Select(x => new SsBulkLink(
                        "SheriffTraining",
                        x.Row.Id,
                        "UserTraining",
                        Key(x.State.Id),
                        x.MappedHash,
                        historyAfter[Key(x.State.Id)].Fingerprint,
                        run.Id,
                        x.Row.Date("UpdatedOn") ?? x.Row.Date("CreatedOn")
                    ))
                    .ToList(),
                ct
            );
            var recordUpdates = new Dictionary<long, SsBulkRecordUpdate>();
            foreach (var (record, changed) in stamps)
                recordUpdates[record.Id] = new(record.Id, record.PayloadHash, record.AfterHash, changed, true);
            var mutations = new List<SsBulkMutation>();
            foreach (var intent in intents)
            {
                if (intent.Kind == "Insert")
                {
                    var write = writes[intent.Index];
                    var image = historyAfter[Key(write.State.Id)];
                    mutations.Add(
                        new(null, "SheriffTraining", write.Row.Id, "Insert", image.Fingerprint, image.Version)
                    );
                }
                else if (intent.Kind == "Update")
                {
                    var write = writes[intent.Index];
                    var record = write.Record!;
                    var image = historyAfter[record.TargetKey];
                    recordUpdates[record.Id] = new(
                        record.Id,
                        write.MappedHash,
                        image.Fingerprint,
                        write.Row.Date("UpdatedOn") ?? write.Row.Date("CreatedOn"),
                        true
                    );
                    mutations.Add(new(record.Id, null, null, "Update", image.Fingerprint, image.Version));
                    foreach (
                        var alias in aliases.Where(x =>
                            x.TargetType == "UserTraining" && x.TargetKey == record.TargetKey && x.Id != record.Id
                        )
                    )
                    {
                        recordUpdates[alias.Id] = recordUpdates.TryGetValue(alias.Id, out var prior)
                            ? prior with
                            {
                                AfterHash = image.Fingerprint,
                            }
                            : new(alias.Id, alias.PayloadHash, image.Fingerprint, alias.SourceUpdatedAtUtc, false);
                        mutations.Add(new(alias.Id, null, null, "GraphRefresh", image.Fingerprint, image.Version));
                    }
                }
                else if (intent.Kind == "Renumber")
                {
                    // Version-only move of an owned row (PerRow: SsWriteSession.RenumberOwnedAsync). Source
                    // hashes stay; the crosswalk after-hash follows the new version so reruns see no drift.
                    var key = Key(intent.Index);
                    var image = historyAfter[key];
                    var linked = aliases.Where(x => x.TargetType == "UserTraining" && x.TargetKey == key).ToList();
                    var owner =
                        linked.FirstOrDefault(x => x.CreatedByRunId is not null)
                        ?? throw new InvalidOperationException("SS bulk renumbered training target is not owned.");
                    recordUpdates[owner.Id] = recordUpdates.TryGetValue(owner.Id, out var ownerPrior)
                        ? ownerPrior with
                        {
                            AfterHash = image.Fingerprint,
                            SetLastApplied = true,
                        }
                        : new(owner.Id, owner.PayloadHash, image.Fingerprint, owner.SourceUpdatedAtUtc, true);
                    mutations.Add(new(owner.Id, null, null, "Update", image.Fingerprint, image.Version));
                    foreach (var alias in linked.Where(x => x.Id != owner.Id))
                    {
                        recordUpdates[alias.Id] = recordUpdates.TryGetValue(alias.Id, out var prior)
                            ? prior with
                            {
                                AfterHash = image.Fingerprint,
                            }
                            : new(alias.Id, alias.PayloadHash, image.Fingerprint, alias.SourceUpdatedAtUtc, false);
                        mutations.Add(new(alias.Id, null, null, "GraphRefresh", image.Fingerprint, image.Version));
                    }
                }
                else
                {
                    var key = composes[intent.Index].User.ToString("D");
                    var image = usersAfter[key];
                    foreach (var alias in aliases.Where(x => x.TargetType == "User" && x.TargetKey == key))
                        mutations.Add(new(alias.Id, null, null, "Compose", image.Fingerprint, image.Version));
                }
            }
            await SsBulkCrosswalk.UpdateRecordsAsync(connection, run.Id, [.. recordUpdates.Values], ct);
            await SsBulkCrosswalk.AppendMutationsAsync(connection, run.Id, mutations, ct);

            var audit = new SsBulkAudit(db);
            audit.Added(nameof(UserTraining), "UserTrainings", inserts.Select(x => historyAfter[Key(x.State.Id)]));
            // A renumbered row is saved once per move in PerRow (each move audits its own version step), so
            // rebuild the intermediate images from the final one.
            SsBulkTargetImage Step(int id, int movesLeft)
            {
                var image = historyAfter[Key(id)];
                if (movesLeft == 0)
                    return image;
                var version = (int)image.PersistedValues["Version"]! - movesLeft;
                return image with
                {
                    AuditValues = WithVersion(image.AuditValues, version),
                    PersistedValues = WithVersion(image.PersistedValues, version),
                };
            }
            foreach (var write in updates)
                audit.Modified(
                    nameof(UserTraining),
                    "UserTrainings",
                    ownedBefore[write.Record!.TargetKey],
                    shiftedWrites.Contains(write.State.Id)
                        ? Step(write.State.Id, renumberEvents.GetValueOrDefault(write.State.Id))
                        : historyAfter[write.Record.TargetKey]
                );
            var seen = new Dictionary<int, int>();
            foreach (var intent in intents.Where(x => x.Kind == "Renumber"))
            {
                var done = seen[intent.Index] = seen.GetValueOrDefault(intent.Index) + 1;
                var total = renumberEvents[intent.Index];
                var before =
                    done == 1 && !shiftedWrites.Contains(intent.Index)
                        ? ownedBefore[Key(intent.Index)]
                        : Step(intent.Index, total - done + 1);
                audit.Modified(nameof(UserTraining), "UserTrainings", before, Step(intent.Index, total - done));
            }
            foreach (var compose in composes)
            {
                var key = compose.User.ToString("D");
                audit.Modified(nameof(User), "Users", usersBefore[key], usersAfter[key]);
            }
            await audit.FlushAsync(ct);
            foreach (var insert in inserts)
                ledger.Insert($"UserTraining:{Key(insert.State.Id)}");
        }

        private async Task<List<Rec>> RecordsAsync(string entityType, string[] legacyIds, CancellationToken ct) =>
            await QueryAsync(
                """
                SELECT "Id","LegacyId","TargetType","TargetKey","PayloadHash","AfterHash","CreatedByRunId","SourceUpdatedAtUtc"
                FROM "DataMigrationRecords"
                WHERE "Source"='SS' AND "EntityType"=@type AND "LegacyId" = ANY(@ids)
                ORDER BY "Id"
                """,
                ReadRec,
                ct,
                new NpgsqlParameter("type", entityType),
                new NpgsqlParameter("ids", legacyIds)
            );
    }

    private static Rec ReadRec(NpgsqlDataReader x) =>
        new(
            x.GetInt64(0),
            x.GetString(1),
            x.GetString(2),
            x.GetString(3),
            x.GetString(4),
            x.IsDBNull(5) ? null : x.GetString(5),
            x.IsDBNull(6) ? null : x.GetGuid(6),
            x.IsDBNull(7) ? null : x.GetFieldValue<DateTimeOffset>(7)
        );

    private static Ut ReadUt(NpgsqlDataReader x) =>
        new()
        {
            Id = x.GetInt32(0),
            UserId = x.GetGuid(1),
            TrainingId = x.GetInt32(2),
            Version = x.GetInt32(3),
            EndingOn = x.GetFieldValue<DateTimeOffset>(4),
            AwardedOn = x.GetFieldValue<DateTimeOffset>(5),
        };

    private static List<Ut> Pair(Dictionary<(Guid, int), List<Ut>> pairs, Guid user, int course)
    {
        if (!pairs.TryGetValue((user, course), out var list))
            pairs[(user, course)] = list = [];
        return list;
    }

    private static string Key(int id) => id.ToString(CultureInfo.InvariantCulture);

    private static IReadOnlyDictionary<string, object?> WithVersion(
        IReadOnlyDictionary<string, object?> values,
        int version
    ) =>
        values.ContainsKey("Version")
            ? new Dictionary<string, object?>(values, StringComparer.Ordinal) { ["Version"] = version }
            : values;
}
