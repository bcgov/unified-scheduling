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

    /// <summary>Global SheriffTraining processing order; version numbering depends on it.</summary>
    internal static IEnumerable<SsRow> HistoryOrder(IEnumerable<SsRow> histories) =>
        histories.OrderBy(x => x.Date("EndDate")).ThenBy(x => x.Date("StartDate")).ThenBy(x => x.Int("Id"));

    /// <summary>True when the sheriff's training is excluded as ExcusedSheriff (Training IncludeExcused off).</summary>
    internal static bool ExcludedExcused(SsRow sheriff, bool includeExcused) =>
        !includeExcused && sheriff.Bool("Excused");

    internal static string ExcludedHistoryReason(bool linked, bool excused) =>
        linked ? "PreviouslyMigratedNowExcludedRetained"
        : excused ? "ExcusedSheriff"
        : "DeletedHistory";

    internal static string HistoryHash(
        SsRow row,
        string sourceUser,
        (DateTimeOffset Start, DateTimeOffset End) dates
    ) =>
        SsWriteSession.Hash(
            new
            {
                User = sourceUser,
                Course = row.Int("TrainingTypeId"),
                dates,
                Expiry = row.Date("TrainingCertificationExpiry"),
                Notice = row.Bool("FirstNotice"),
            }
        );

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
}
