using System.Globalization;
using Unified.DataMigration.Sources;

namespace Unified.DataMigration.Services;

/// <summary>
/// Pure SS Training classification/mapping rules shared by the PerRow writer
/// (<see cref="SsTrainingWriter"/>) and the Bulk writer (<see cref="Bulk.SsTrainingBulkWriter"/>).
/// Nothing here touches the database; ordering of rejection checks is part of the contract.
/// </summary>
internal static class SsTrainingRules
{
    internal const int CourseCodeMaxLength = 50;
    internal const int CourseDescriptionMaxLength = 200;
    internal const int UserTrainingNotesMaxLength = 2000;

    internal static readonly IReadOnlySet<string> CarbineCourses = new HashSet<string>(StringComparer.Ordinal)
    {
        "Carbine Initial Course",
        "Carbine Practice 1",
        "Carbine Practice 2",
        "Carbine Annual Recertification",
    };

    internal sealed record CourseMapping(
        string Code,
        string Description,
        int Validity,
        int Notice,
        int? Days,
        string Hash
    );

    internal static IEnumerable<SsRow> Courses(IEnumerable<SsRow> lookupCodes) =>
        lookupCodes.Where(x => x.Int("Type") == 6).OrderBy(x => x.Int("Id"));

    internal static HashSet<string> SourceCourseCodes(IEnumerable<SsRow> lookupCodes) =>
        lookupCodes
            .Where(x => x.Int("Type") == 6)
            .Select(x => x.Text("Code"))
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Course rows per exact (ordinal) code; null codes never match a non-blank course code.
    /// Build once per run and pass to <see cref="MapCourse"/>.
    /// </summary>
    internal static Dictionary<string, int> CourseCodeCounts(IEnumerable<SsRow> lookupCodes) =>
        lookupCodes
            .Where(x => x.Int("Type") == 6)
            .Select(x => x.Text("Code"))
            .OfType<string>()
            .CountBy(x => x, StringComparer.Ordinal)
            .ToDictionary(StringComparer.Ordinal);

    internal static HashSet<int?> ShortenedCarbineCourseIds(IEnumerable<SsRow> lookupCodes) =>
        lookupCodes
            .Where(x =>
                x.Int("Type") == 6
                && x.Text("Code") is { Length: > CourseCodeMaxLength } code
                && code.Contains("Carbine", StringComparison.Ordinal)
            )
            .Select(x => x.Int("Id"))
            .ToHashSet();

    /// <summary>
    /// Courses with a repeated eligible (user, course) history are rotating. Training histories have
    /// no history date limit, so eligibility does not depend on HistoryStartDate. Excused users' histories
    /// are eligible only when the Training job includes excused users.
    /// </summary>
    internal static HashSet<int?> RepeatedCourses(
        IEnumerable<SsRow> histories,
        IReadOnlyDictionary<string, SsRow> users,
        bool includeExcused
    ) =>
        histories
            .Where(history =>
                history.Date("ExpiryDate") is null
                && users.TryGetValue(history.Guid("SheriffId")?.ToString() ?? "", out var user)
                && !ExcludedExcused(user, includeExcused)
            )
            .GroupBy(x => (User: x.Guid("SheriffId"), Course: x.Int("TrainingTypeId")))
            .Where(x => x.Count() > 1)
            .Select(x => x.Key.Course)
            .ToHashSet();

    /// <summary>Validates and maps one course source row. Throws <see cref="SsReject"/> in contract order.</summary>
    internal static CourseMapping MapCourse(SsRow row, IReadOnlyDictionary<string, int> courseCodeCounts)
    {
        var code = row.Text("Code");
        if (string.IsNullOrWhiteSpace(code))
            throw new SsReject("InvalidCourseCode");
        if (row.Int("LocationId") is not null || row.Text("SubCode") is not null)
            throw new SsReject("UnsupportedScopedCourse");
        if (courseCodeCounts.GetValueOrDefault(code) != 1)
            throw new SsReject("DuplicateCourseCode");
        var description = row.Text("Description") ?? "";
        if (description.Length > CourseDescriptionMaxLength)
            throw new SsReject("CourseDescriptionTooLong");
        var validity = row.Int("ValidityPeriod") ?? 0;
        var notice = row.Int("AdvanceNotice") ?? 0;
        if (validity < 0 || notice < 0)
            throw new SsReject("NegativeTrainingPeriod");
        int? days =
            validity == 0 ? null
            : validity == 1461 ? 1460
            : Math.Min(validity, 1825);
        if (notice > 0 && days is not null && notice >= days)
            throw new SsReject("InvalidAdvanceNotice");
        var hash = SsWriteSession.Hash(
            new
            {
                code,
                days,
                notice,
                Mandatory = row.Bool("Mandatory"),
                Rotating = row.Bool("Rotating"),
                Effective = row.Date("EffectiveDate"),
                Expiry = row.Date("ExpiryDate"),
                Category = row.Text("Category"),
            }
        );
        return new(code, description, validity, notice, days, hash);
    }

    internal static string AllocateCourseCode(string code, IReadOnlySet<string> reserved)
    {
        if (code.Length <= CourseCodeMaxLength)
            return code;
        var prefixLength = char.IsHighSurrogate(code[44]) && char.IsLowSurrogate(code[45]) ? 44 : 45;
        var prefix = code[..prefixLength];
        for (var suffix = 1; ; suffix++)
        {
            var candidate = prefix + "_" + suffix.ToString(CultureInfo.InvariantCulture);
            if (candidate.Length > CourseCodeMaxLength)
                throw new SsReject("CourseCodeSuffixExhausted");
            if (!reserved.Contains(candidate))
                return candidate;
        }
    }

    /// <summary>Returns the approved category name (or null) for a new course.</summary>
    internal static string? ApprovedCategory(SsRow row)
    {
        var category = row.Text("Category");
        if (string.IsNullOrWhiteSpace(category))
            return null;
        if (category is not ("Provincial Standard" or "Branch" or "Local") || category.Length > 100)
            throw new SsReject("UnapprovedTrainingCategory");
        return category;
    }

    internal static bool Rotating(SsRow row, CourseMapping mapping, IReadOnlySet<int?> repeats) =>
        row.Bool("Rotating") || mapping.Validity > 0 || repeats.Contains(row.Int("Id"));

    internal static int? AdvanceNoticeDays(CourseMapping mapping) => mapping.Notice == 0 ? null : mapping.Notice;

    internal static (string Outcome, string? Reason) CourseLinkOutcome(bool shortened) =>
        shortened ? ("MigratedWithDifference", "CourseCodeShortened") : ("Migrated", null);

    /// <summary>
    /// Global SheriffTraining processing order; version numbering depends on it. Ordered by the mapped
    /// (Vancouver-adjusted, see <see cref="MapHistoryTimes"/>) EndDate, StartDate, then source ID so the order
    /// uses the same values <see cref="PlaceHistory"/> compares. Rows with missing dates keep sorting first
    /// (they reject later).
    /// </summary>
    internal static IEnumerable<SsRow> HistoryOrder(
        IEnumerable<SsRow> histories,
        IReadOnlySet<string> vancouverSheriffs
    ) =>
        histories
            .Select(x => (Row: x, Key: HistoryOrderKey(x, IsVancouverSheriff(x, vancouverSheriffs))))
            .OrderBy(x => x.Key.End)
            .ThenBy(x => x.Key.Start)
            .ThenBy(x => x.Row.Int("Id"))
            .Select(x => x.Row);

    private static (DateTimeOffset? Start, DateTimeOffset? End) HistoryOrderKey(SsRow row, bool vancouver)
    {
        var start = row.Date("StartDate");
        var end = row.Date("EndDate");
        if (start is null || end is null || end < start)
            return (
                start is null ? null : AdjustVancouverStandardTime(start.Value, vancouver),
                end is null ? null : AdjustVancouverStandardTime(end.Value, vancouver)
            );
        var mapped = MapTimes(start.Value, end.Value, null, vancouver);
        return (mapped.Start, mapped.End);
    }

    // ---------------------------------------------------------------- America/Vancouver permanent UTC-07:00

    /// <summary>The only SS Location.Timezone value whose trainings are adjusted (trimmed, ordinal).</summary>
    internal const string VancouverTimeZone = "America/Vancouver";

    /// <summary>
    /// First instant of the permanent UTC-07:00 offset: the old-rule fall-back 2026-11-01 02:00 PDT (09:00Z).
    /// Unified renders instants before it with the historical BC rules (-08:00 in standard time), which already
    /// matches what SS showed, so only instants at or after it are adjusted.
    /// </summary>
    internal static readonly DateTimeOffset VancouverPermanentOffsetStart = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>True when the SS location time zone is exactly <c>America/Vancouver</c> (after trimming).</summary>
    internal static bool IsVancouverZone(string? zone) =>
        zone is not null && string.Equals(zone.Trim(), VancouverTimeZone, StringComparison.Ordinal);

    /// <summary>
    /// SS user IDs whose home location (User.HomeLocationId -> Location.Timezone, the zone SS displays and
    /// enters training in) is America/Vancouver. A missing home location or Location row means no adjustment.
    /// </summary>
    internal static HashSet<string> VancouverSheriffs(IEnumerable<SsRow> users, IEnumerable<SsRow> locations)
    {
        var vancouverLocations = locations
            .Where(x => IsVancouverZone(x.Text("Timezone")))
            .Select(x => x.Id)
            .ToHashSet(StringComparer.Ordinal);
        return users
            .Where(x =>
                x.Int("HomeLocationId") is { } home
                && vancouverLocations.Contains(home.ToString(CultureInfo.InvariantCulture))
            )
            .Select(x => x.Id)
            .ToHashSet(StringComparer.Ordinal);
    }

    internal static bool IsVancouverSheriff(SsRow history, IReadOnlySet<string> vancouverSheriffs) =>
        history.Guid("SheriffId")?.ToString() is { } sheriff && vancouverSheriffs.Contains(sheriff);

    /// <summary>
    /// Standard time under the old (pre-2026-11-01) Pacific rules: DST from the 2nd Sunday of March 02:00 PST
    /// (10:00Z) to the 1st Sunday of November 02:00 PDT (09:00Z). SS stored such instants with UTC-08:00.
    /// </summary>
    internal static bool IsOldPacificStandardTime(DateTimeOffset instant)
    {
        var utc = instant.UtcDateTime;
        var dstStart = NthSunday(utc.Year, 3, 2).AddHours(10);
        var dstEnd = NthSunday(utc.Year, 11, 1).AddHours(9);
        return utc < dstStart || utc >= dstEnd;
    }

    private static DateTime NthSunday(int year, int month, int n)
    {
        var first = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var offset = ((int)DayOfWeek.Sunday - (int)first.DayOfWeek + 7) % 7;
        return first.AddDays(offset + 7 * (n - 1));
    }

    /// <summary>
    /// SS stored an America/Vancouver standard-time wall clock as local+8h; Unified renders it at UTC-07:00 from
    /// 2026-11-01, so the correct instant is local+7h: subtract one hour. Example: SS 2026-12-01 09:00 PST was
    /// stored as 17:00Z (Unified would show 10:00); mapped to 16:00Z (Unified shows 09:00).
    /// Old-rule DST instants, instants before <see cref="VancouverPermanentOffsetStart"/> and non-Vancouver
    /// zones are unchanged.
    /// </summary>
    internal static DateTimeOffset AdjustVancouverStandardTime(DateTimeOffset instant, bool vancouver) =>
        vancouver && instant >= VancouverPermanentOffsetStart && IsOldPacificStandardTime(instant)
            ? instant.AddHours(-1)
            : instant;

    /// <summary>
    /// Mapped UserTraining instants. <see cref="Adjusted"/> is true when any value differs from the source.
    /// </summary>
    internal sealed record HistoryTimes(
        DateTimeOffset Start,
        DateTimeOffset End,
        DateTimeOffset? Expiry,
        bool Adjusted
    );

    /// <summary>
    /// Validates SS StartDate/EndDate (<see cref="SsWriteSession.Dates"/>) and maps StartDate, EndDate and
    /// TrainingCertificationExpiry with <see cref="AdjustVancouverStandardTime"/>. Each endpoint uses its own
    /// old-rule offset (an interval spanning a DST transition shifts only its standard-time endpoint). Only the
    /// 2026-11-01 fall-back hour can reverse an interval (start in PDT, end in the repeated PST hour); the end
    /// is then clamped to the start so EndingOn &gt;= AwardedOn still holds.
    /// </summary>
    internal static HistoryTimes MapHistoryTimes(SsRow row, bool vancouver)
    {
        var (start, end) = SsWriteSession.Dates(row);
        return MapTimes(start, end, row.Date("TrainingCertificationExpiry"), vancouver);
    }

    /// <summary>
    /// Times an owned update writes to an already-migrated UserTraining. The Vancouver adjustment manages only
    /// incoming SS values; times already in Unified are never adjusted (they were corrected by hand). Each
    /// value keeps the target's current instant while it still represents the SS value (equals the raw SS
    /// instant or its <paramref name="mapped"/> instant); only an SS value that changed is replaced, with its
    /// mapped instant. The end is clamped to the start if a kept/replaced mix would reverse the interval.
    /// </summary>
    internal static HistoryTimes KeepTargetTimes(
        SsRow row,
        HistoryTimes mapped,
        DateTimeOffset targetStart,
        DateTimeOffset targetEnd,
        DateTimeOffset? targetExpiry
    )
    {
        var (start, end) = SsWriteSession.Dates(row);
        var expiry = row.Date("TrainingCertificationExpiry");
        var keptStart = KeepTarget(targetStart, start, mapped.Start);
        var keptEnd = KeepTarget(targetEnd, end, mapped.End);
        if (keptEnd < keptStart)
            keptEnd = keptStart;
        var keptExpiry = KeepTarget(targetExpiry, expiry, mapped.Expiry);
        return new(keptStart, keptEnd, keptExpiry, keptStart != start || keptEnd != end || keptExpiry != expiry);
    }

    private static T KeepTarget<T>(T target, T source, T mapped) =>
        EqualityComparer<T>.Default.Equals(target, source) || EqualityComparer<T>.Default.Equals(target, mapped)
            ? target
            : mapped;

    internal static HistoryTimes MapTimes(
        DateTimeOffset start,
        DateTimeOffset end,
        DateTimeOffset? expiry,
        bool vancouver
    )
    {
        var mappedStart = AdjustVancouverStandardTime(start, vancouver);
        var mappedEnd = AdjustVancouverStandardTime(end, vancouver);
        if (mappedEnd < mappedStart)
            mappedEnd = mappedStart;
        var mappedExpiry = expiry is { } value ? AdjustVancouverStandardTime(value, vancouver) : (DateTimeOffset?)null;
        return new(
            mappedStart,
            mappedEnd,
            mappedExpiry,
            mappedStart != start || mappedEnd != end || mappedExpiry != expiry
        );
    }

    /// <summary>Mapped-value image of a UserTraining (record equality compares instants, ordinal text).</summary>
    internal sealed record HistoryTargetValues(
        Guid UserId,
        int TrainingId,
        DateTimeOffset AwardedOn,
        DateTimeOffset EndingOn,
        bool AllDay,
        DateTimeOffset? ExpiryDate,
        string? Notes,
        string? NoticeState
    );

    /// <summary>What the job would write for <paramref name="row"/> (migrated rows are always timed).</summary>
    internal static HistoryTargetValues MappedTarget(
        SsRow row,
        Guid userId,
        int trainingId,
        string courseCode,
        HistoryTimes times
    ) =>
        new(
            userId,
            trainingId,
            times.Start,
            times.End,
            false,
            times.Expiry,
            MappedNotes(row, courseCode),
            NoticeState(row)
        );

    /// <summary>True when the sheriff's training is excluded as ExcusedSheriff (Training IncludeExcused off).</summary>
    internal static bool ExcludedExcused(SsRow sheriff, bool includeExcused) =>
        !includeExcused && sheriff.Bool("Excused");

    internal static string ExcludedHistoryReason(bool linked, bool excused) =>
        linked ? "PreviouslyMigratedNowExcludedRetained"
        : excused ? "ExcusedSheriff"
        : "DeletedHistory";

    /// <summary>
    /// Mapped history payload hash (original shape, byte for byte). It hashes only raw SS values, never the
    /// Vancouver-adjusted instants, so the adjustment alone never makes an already-migrated history look
    /// changed: UserTrainings already in Unified (corrected by hand) are not rewritten because of it.
    /// </summary>
    internal static string HistoryHash(SsRow row, string sourceUser)
    {
        // Kept for hash compatibility; System.Text.Json serializes the ValueTuple (fields only) as {}.
        var dates = (Start: row.Date("StartDate"), End: row.Date("EndDate"));
        return SsWriteSession.Hash(
            new
            {
                User = sourceUser,
                Course = row.Int("TrainingTypeId"),
                dates,
                Expiry = row.Date("TrainingCertificationExpiry"),
                Notice = row.Bool("FirstNotice"),
            }
        );
    }

    internal static string? MappedNotes(SsRow row, string courseCode)
    {
        var comment = row.Text("Comment");
        var note = row.Text("Note");
        if (courseCode != "Other")
        {
            if (!string.IsNullOrEmpty(comment))
                throw new SsReject("UnexpectedNonOtherComment");
            return note;
        }
        return string.IsNullOrEmpty(comment)
            ? note
            : $"Other training: {comment}" + (string.IsNullOrEmpty(note) || note == comment ? "" : "\n" + note);
    }

    internal static string NoticeState(SsRow row) =>
        row.Bool("FirstNotice")
            ? Unified.Db.Models.Training.UserTrainingNoticeStates.Sent
            : Unified.Db.Models.Training.UserTrainingNoticeStates.None;

    /// <summary>
    /// Carbine histories keep the source keyword even when shortening removes it from the target label.
    /// </summary>
    internal static bool IsCarbineHistory(string targetCourseCode, int? sourceCourseId, IReadOnlySet<int?> shortened) =>
        targetCourseCode.Contains("Carbine", StringComparison.Ordinal) || shortened.Contains(sourceCourseId);

    internal static string HistoryLinkReason(SsRow row, bool olderHistoryExists) =>
        row.Text("Timezone") is not null ? "TargetTrainingTimezoneUnavailable"
        : olderHistoryExists ? "LateOlderHistoryAppended"
        : "TargetTrainingAllDayNotInSource";

    internal static int NextVersion(int? max) =>
        max == int.MaxValue ? throw new SsReject("VersionOverflow")
        : max is null ? 1
        : max.Value + 1;

    /// <summary>
    /// One existing UserTraining in the new history's (user, course) group. <paramref name="LegacyId"/> is the
    /// owning SheriffTraining ID (null for Unified-created rows).
    /// </summary>
    internal sealed record HistorySlot(
        int Id,
        int Version,
        DateTimeOffset AwardedOn,
        DateTimeOffset EndingOn,
        string? LegacyId
    );

    /// <summary>
    /// Where a new history goes: <paramref name="Version"/> for the new row, <paramref name="Shift"/> the
    /// existing row IDs to move up one version (highest version first), or <paramref name="Skip"/> a reason.
    /// </summary>
    internal sealed record HistoryPlacement(int Version, IReadOnlyList<int> Shift, string? Skip);

    /// <summary>Unified-created or linked-only newer row prevents an older SS history from taking a lower version.</summary>
    internal const string UnownedNewerHistoryReason = "OlderHistoryBlockedByUnownedVersion";

    private static int? SourceId(string? legacyId) =>
        int.TryParse(legacyId, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    /// <summary>
    /// True when <paramref name="x"/> sorts after a history with <paramref name="start"/>/<paramref name="end"/>
    /// and source ID <paramref name="id"/> in the first-run (EndDate, StartDate, source ID) order.
    /// </summary>
    private static bool SortsAfter(HistorySlot x, int? id, DateTimeOffset start, DateTimeOffset end)
    {
        if (x.EndingOn != end)
            return x.EndingOn > end;
        if (x.AwardedOn != start)
            return x.AwardedOn > start;
        // Unified-created rows (no source ID) are treated as newer on an exact tie.
        return x.LegacyId is null || id is null || SourceId(x.LegacyId) is not { } other || other > id;
    }

    /// <summary>
    /// True when moving an existing history from (<paramref name="oldStart"/>, <paramref name="oldEnd"/>) to
    /// (<paramref name="start"/>, <paramref name="end"/>) changes which of the <paramref name="others"/> in its
    /// user/course group sort after it, i.e. its version must be placed again (an owned update applying
    /// changed SS dates). Same dates or same relative order: keep the version.
    /// </summary>
    internal static bool HistoryOrderChanged(
        IEnumerable<HistorySlot> others,
        string legacyId,
        DateTimeOffset oldStart,
        DateTimeOffset oldEnd,
        DateTimeOffset start,
        DateTimeOffset end
    )
    {
        if (oldStart == start && oldEnd == end)
            return false;
        var id = SourceId(legacyId);
        return others.Any(x => SortsAfter(x, id, oldStart, oldEnd) != SortsAfter(x, id, start, end));
    }

    /// <summary>
    /// Only a row ending at or after the new history can sort after it; otherwise the cheap max+1 path applies.
    /// </summary>
    internal static bool MayHaveNewerHistory(IEnumerable<DateTimeOffset> endings, DateTimeOffset end) =>
        endings.Any(x => x >= end);

    /// <summary>
    /// Version for a new SheriffTraining history so the group keeps the first-run <see cref="HistoryOrder"/>
    /// (EndDate, StartDate, source ID): the current certificate stays the highest version. With no newer
    /// row it is max+1 (a clean first run never renumbers). Otherwise the new row takes the lowest newer
    /// version and the contiguous run of versions from there moves up one, but only when every row in that
    /// run is migration-owned and undrifted. If not, the new row takes the highest free version strictly
    /// between every older row and the newer rows without touching them, or is skipped with the blocking
    /// row's reason (OwnedTargetDrift wins).
    /// <paramref name="block"/> is evaluated only for rows that would move: null when the row is
    /// migration-owned, version-proven and undrifted (the job may renumber it), otherwise the reason it may not.
    /// </summary>
    internal static HistoryPlacement PlaceHistory(
        IReadOnlyCollection<HistorySlot> group,
        string legacyId,
        DateTimeOffset start,
        DateTimeOffset end,
        Func<HistorySlot, string?> block
    )
    {
        var id = SourceId(legacyId);
        bool Newer(HistorySlot x) => SortsAfter(x, id, start, end);
        var newer = group.Where(Newer).ToList();
        if (newer.Count == 0)
            return new(NextVersion(group.Select(x => (int?)x.Version).Max()), [], null);
        var lowest = newer.Min(x => x.Version);
        var byVersion = group.ToDictionary(x => x.Version);
        var run = new List<HistorySlot>();
        for (var version = lowest; byVersion.TryGetValue(version, out var slot); version++)
        {
            if (version == int.MaxValue)
                throw new SsReject("VersionOverflow");
            run.Add(slot);
        }
        var blocks = run.Select(x => block(x)).Where(x => x is not null).ToList();
        if (blocks.Count == 0)
            return new(lowest, [.. run.OrderByDescending(x => x.Version).Select(x => x.Id)], null);
        // The free version must also sit above every history that sorts before the new row (owned or not),
        // so the chronological order holds on both sides; a gap below an older row is not usable.
        var floor = group.Where(x => !Newer(x)).Select(x => x.Version).DefaultIfEmpty(0).Max();
        for (var version = lowest - 1; version > floor; version--)
            if (!byVersion.ContainsKey(version))
                return new(version, [], null);
        return new(0, [], blocks.Contains("OwnedTargetDrift") ? "OwnedTargetDrift" : blocks[0]);
    }
}
