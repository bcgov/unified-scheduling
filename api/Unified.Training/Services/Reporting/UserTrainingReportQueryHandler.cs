using Microsoft.EntityFrameworkCore;
using Unified.Common.Reporting;
using Unified.Db;
using Unified.Training.Mappings;

namespace Unified.Training.Services.Reporting;

public sealed class UserTrainingReportQueryHandler(UnifiedDbContext db) : IReportQueryHandler
{
    private const string UserIdFilterKey = "userId";
    private const string TrainingIdFilterKey = "trainingId";
    private const string TrainingCodeFilterKey = "trainingCode";
    private const string StatusFilterKey = "status";
    private const string StartDateFilterKey = "startDate";
    private const string EndDateFilterKey = "endDate";

    public string ReportKey => "user-training";

    private static IReadOnlyCollection<IReadOnlyDictionary<string, object?>> Columns =>
    [
        BuildColumn("userDisplayName", "User", "String"),
        BuildColumn("trainingId", "ID", "Number"),
        BuildColumn("trainingCode", "Code", "String"),
        BuildColumn("trainingDescription", "Description", "String"),
        BuildColumn("trainingCategory", "Category", "String"),
        BuildColumn("awardedOn", "Awarded On", "DateTime"),
        BuildColumn("endingOn", "Ending On", "DateTime"),
        BuildColumn("expiryDate", "Expiry Date", "DateTime"),
        BuildColumn("status", "Status", "String", sortable: false),
        BuildColumn("version", "Version", "Number"),
        BuildColumn("noticeState", "Notice State", "String"),
        BuildColumn("notes", "Notes", "String", sortable: false),
    ];

    public async Task<(
        IReadOnlyCollection<IReadOnlyDictionary<string, object?>> Columns,
        IReadOnlyCollection<IReadOnlyDictionary<string, object?>> Rows,
        int TotalRows
    )> ExecuteAsync(
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> filters,
        int page,
        int pageSize,
        string? sortBy,
        string? sortDirection,
        string? timeZone,
        CancellationToken cancellationToken = default
    )
    {
        var now = DateTimeOffset.UtcNow;
        var parsedFilters = ParseFilters(filters);

        var query = db
            .UserTrainings.AsNoTracking()
            .Where(ut => ut.Training.ExpiryDate == null || ut.Training.ExpiryDate > now);

        query = parsedFilters.UserId is Guid userId
            ? query.Where(ut => ut.UserId == userId)
            : query;

        query = parsedFilters.TrainingId is int trainingId
            ? query.Where(ut => ut.TrainingId == trainingId)
            : query;

        query = parsedFilters.NormalizedTrainingCode is string trainingCode
            ? query.Where(ut => ut.Training.Code.ToLower().Contains(trainingCode))
            : query;

        query = parsedFilters.StartDateInclusive is DateTimeOffset startDateInclusive
            ? query.Where(ut => ut.AwardedOn >= startDateInclusive)
            : query;

        query = parsedFilters.EndDateExclusive is DateTimeOffset endDateExclusive
            ? query.Where(ut => ut.AwardedOn < endDateExclusive)
            : query;

        query = parsedFilters.Status switch
        {
            TrainingCompletionStatus.Active => query.Where(ut => ut.ExpiryDate == null || ut.ExpiryDate > now),
            TrainingCompletionStatus.Expired => query.Where(ut => ut.ExpiryDate != null && ut.ExpiryDate <= now),
            _ => query,
        };

        query = query.Where(ut =>
            ut.Version
            == db
                .UserTrainings.Where(candidate =>
                    candidate.UserId == ut.UserId && candidate.TrainingId == ut.TrainingId
                )
                .Max(candidate => candidate.Version)
        );

        var projectedQuery = query.Select(ut => new UserTrainingReportRow(
            ut.UserId,
            ut.User.FirstName,
            ut.User.LastName,
            ut.TrainingId,
            ut.Training.Code,
            ut.Training.Description,
            ut.Training.TrainingCategory != null ? ut.Training.TrainingCategory.Name : string.Empty,
            ut.AwardedOn,
            ut.EndingOn,
            ut.ExpiryDate,
            ut.Version,
            ut.NoticeState,
            ut.Notes,
            false
        ));

        var reportRows = await projectedQuery.ToListAsync(cancellationToken);

        if (parsedFilters.ShouldIncludeMissingMandatoryRows)
        {
            var usersQuery = db.Users.AsNoTracking().Where(user => user.IsEnabled);
            usersQuery = parsedFilters.UserId is Guid reportUserId
                ? usersQuery.Where(user => user.Id == reportUserId)
                : usersQuery;

            var mandatoryTrainingsQuery = db
                .Trainings.AsNoTracking()
                .Where(training =>
                    training.Mandatory && (training.ExpiryDate == null || training.ExpiryDate > now)
                );

            mandatoryTrainingsQuery = parsedFilters.TrainingId is int reportTrainingId
                ? mandatoryTrainingsQuery.Where(training => training.Id == reportTrainingId)
                : mandatoryTrainingsQuery;

            mandatoryTrainingsQuery = parsedFilters.NormalizedTrainingCode is string mandatoryTrainingCode
                ? mandatoryTrainingsQuery.Where(training => training.Code.ToLower().Contains(mandatoryTrainingCode))
                : mandatoryTrainingsQuery;

            var missingMandatoryRows = await (
                from user in usersQuery
                from training in mandatoryTrainingsQuery
                where !db.UserTrainings.Any(ut => ut.UserId == user.Id && ut.TrainingId == training.Id)
                select new UserTrainingReportRow(
                    user.Id,
                    user.FirstName,
                    user.LastName,
                    training.Id,
                    training.Code,
                    training.Description,
                    training.TrainingCategory != null ? training.TrainingCategory.Name : string.Empty,
                    null,
                    null,
                    null,
                    null,
                    string.Empty,
                    string.Empty,
                    true
                )
            ).ToListAsync(cancellationToken);

            reportRows.AddRange(missingMandatoryRows);
        }

        var sortedRows = UserTrainingReportSorting.Apply(reportRows, sortBy, sortDirection);
        var totalRows = sortedRows.Count;
        var pageRows = sortedRows
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var rows = pageRows
            .Select(row =>
                (IReadOnlyDictionary<string, object?>)
                    UserTrainingReportMappings.ToReportRowDictionary(row, GetStatus(row, now))
            )
            .ToArray();

        return (Columns, rows, totalRows);
    }

    private static UserTrainingReportFilters ParseFilters(
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> filters
    )
    {
        var userId = ParseGuidFilter(filters, UserIdFilterKey);
        var trainingId = ParseIntFilter(filters, TrainingIdFilterKey);
        var normalizedTrainingCode = NormalizeForContains(ParseStringFilter(filters, TrainingCodeFilterKey));
        var status = ParseStatusFilter(filters);
        var startDate = ParseDateFilter(filters, StartDateFilterKey);
        var endDate = ParseDateFilter(filters, EndDateFilterKey);

        if (startDate.HasValue && endDate.HasValue && startDate > endDate)
        {
            throw new ArgumentException("Filter 'startDate' must be on or before 'endDate'.");
        }

        var startDateInclusive = startDate.HasValue
            ? new DateTimeOffset(startDate.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
            : (DateTimeOffset?)null;

        var endDateExclusive = endDate.HasValue
            ? new DateTimeOffset(endDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
            : (DateTimeOffset?)null;

        return new UserTrainingReportFilters(
            userId,
            trainingId,
            normalizedTrainingCode,
            status,
            startDateInclusive,
            endDateExclusive
        );
    }

    private static string? NormalizeForContains(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
    }

    private static string GetStatus(UserTrainingReportRow row, DateTimeOffset now)
    {
        if (row.IsMissingMandatoryTrainingAssignment)
        {
            return "Not Taken";
        }

        return row.ExpiryDate == null || row.ExpiryDate > now ? "Active" : "Expired";
    }

    private static string? ParseStringFilter(
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> filters,
        string filterKey
    )
    {
        if (!filters.TryGetValue(filterKey, out var values))
        {
            return null;
        }

        var value = values.FirstOrDefault(entry => !string.IsNullOrWhiteSpace(entry));
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static Guid? ParseGuidFilter(
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> filters,
        string filterKey
    )
    {
        return ParseFilter<Guid>(
            filters,
            filterKey,
            Guid.TryParse,
            $"Filter '{filterKey}' must be a valid GUID."
        );
    }

    private static int? ParseIntFilter(
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> filters,
        string filterKey
    )
    {
        return ParseFilter<int>(
            filters,
            filterKey,
            TryParsePositiveInt,
            $"Filter '{filterKey}' must be a positive integer."
        );
    }

    private static DateOnly? ParseDateFilter(
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> filters,
        string filterKey
    )
    {
        return ParseFilter<DateOnly>(
            filters,
            filterKey,
            DateOnly.TryParse,
            $"Filter '{filterKey}' must be a valid date in YYYY-MM-DD format."
        );
    }

    private static T? ParseFilter<T>(
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> filters,
        string filterKey,
        TryParseFilterValue<T> tryParse,
        string errorMessage
    )
        where T : struct
    {
        var rawValue = ParseStringFilter(filters, filterKey);
        if (rawValue is null)
        {
            return null;
        }

        if (tryParse(rawValue, out var parsed))
        {
            return parsed;
        }

        throw new ArgumentException(errorMessage);
    }

    private static bool TryParsePositiveInt(string rawValue, out int parsed)
    {
        var success = int.TryParse(rawValue, out parsed) && parsed > 0;

        if (!success)
        {
            parsed = default;
        }

        return success;
    }

    private static TrainingCompletionStatus? ParseStatusFilter(
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> filters
    )
    {
        var rawStatus = ParseStringFilter(filters, StatusFilterKey);
        if (rawStatus is null)
        {
            return null;
        }

        return rawStatus.Trim().ToLowerInvariant() switch
        {
            "active" => TrainingCompletionStatus.Active,
            "expired" => TrainingCompletionStatus.Expired,
            _ => throw new ArgumentException("Filter 'status' must be either 'active' or 'expired'."),
        };
    }

    private enum TrainingCompletionStatus
    {
        Active,
        Expired,
    }

    private readonly record struct UserTrainingReportFilters(
        Guid? UserId,
        int? TrainingId,
        string? NormalizedTrainingCode,
        TrainingCompletionStatus? Status,
        DateTimeOffset? StartDateInclusive,
        DateTimeOffset? EndDateExclusive
    )
    {
        public bool ShouldIncludeMissingMandatoryRows =>
            Status is null && !StartDateInclusive.HasValue && !EndDateExclusive.HasValue;
    }

    private static IReadOnlyDictionary<string, object?> BuildColumn(
        string key,
        string label,
        string type,
        bool sortable = true
    ) => new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
    {
        ["key"] = key,
        ["label"] = label,
        ["type"] = type,
        ["sortable"] = sortable,
    };

    private delegate bool TryParseFilterValue<T>(string rawValue, out T parsed);
}
