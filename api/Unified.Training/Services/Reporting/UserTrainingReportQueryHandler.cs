using Microsoft.EntityFrameworkCore;
using Unified.Common.Reporting;
using Unified.Db;
using Unified.Training.Mappings;

namespace Unified.Training.Services.Reporting;

public sealed class UserTrainingReportQueryHandler(UnifiedDbContext db) : ReportQueryHandlerBase, IReportQueryHandler
{
    private const string UserIdFilterKey = "userId";
    private const string TrainingIdFilterKey = "trainingId";
    private const string TrainingCodeFilterKey = "trainingCode";
    private const string StatusFilterKey = "status";
    private const string StartDateFilterKey = "startDate";
    private const string EndDateFilterKey = "endDate";

    public string ReportKey => "user-training";

    public async Task<PaginatableResponse> ExecuteAsync(
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
        var queryFilters = ParseQuery(filters);

        var query = db
            .UserTrainings.AsNoTracking()
            .Where(ut => ut.Training.ExpiryDate == null || ut.Training.ExpiryDate > now);

        query = queryFilters.UserId is Guid userId ? query.Where(ut => ut.UserId == userId) : query;

        query = queryFilters.TrainingId is int trainingId ? query.Where(ut => ut.TrainingId == trainingId) : query;

        query = queryFilters.NormalizedTrainingCode is string trainingCode
            ? query.Where(ut => ut.Training.Code.ToLower().Contains(trainingCode))
            : query;

        query = queryFilters.StartDateInclusive is DateTimeOffset startDateInclusive
            ? query.Where(ut => ut.AwardedOn >= startDateInclusive)
            : query;

        query = queryFilters.EndDateExclusive is DateTimeOffset endDateExclusive
            ? query.Where(ut => ut.AwardedOn < endDateExclusive)
            : query;

        query = queryFilters.Status switch
        {
            TrainingCompletionStatus.Active => query.Where(ut => ut.ExpiryDate == null || ut.ExpiryDate > now),
            TrainingCompletionStatus.Expired => query.Where(ut => ut.ExpiryDate != null && ut.ExpiryDate <= now),
            _ => query,
        };

        query = query.Where(ut =>
            ut.Version
            == db.UserTrainings.Where(candidate =>
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

        if (queryFilters.ShouldIncludeMissingMandatoryRows)
        {
            var usersQuery = db.Users.AsNoTracking().Where(user => user.IsEnabled);
            usersQuery = queryFilters.UserId is Guid reportUserId
                ? usersQuery.Where(user => user.Id == reportUserId)
                : usersQuery;

            var mandatoryTrainingsQuery = db
                .Trainings.AsNoTracking()
                .Where(training => training.Mandatory && (training.ExpiryDate == null || training.ExpiryDate > now));

            mandatoryTrainingsQuery = queryFilters.TrainingId is int reportTrainingId
                ? mandatoryTrainingsQuery.Where(training => training.Id == reportTrainingId)
                : mandatoryTrainingsQuery;

            mandatoryTrainingsQuery = queryFilters.NormalizedTrainingCode is string mandatoryTrainingCode
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

        var sortedRows = ApplySorting(reportRows, sortBy, sortDirection);
        var totalRows = sortedRows.Count;
        var pageRows = sortedRows.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var rows = pageRows
            .Select(row => UserTrainingReportMappings.ToReportRowValue(row, GetStatus(row, now)))
            .ToArray();

        return new UserTrainingReportResponse(rows, page, pageSize, totalRows);
    }

    private static List<UserTrainingReportRow> ApplySorting(
        IEnumerable<UserTrainingReportRow> rows,
        string? sortBy,
        string? sortDirection
    )
    {
        var normalizedSortBy = NormalizeSortBy(sortBy);
        var isDescending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        return normalizedSortBy switch
        {
            "userdisplayname" => ApplyNameSort(rows, isDescending),
            "trainingcode" => ApplySort(rows, row => row.TrainingCode, isDescending),
            "trainingdescription" => ApplySort(rows, row => row.TrainingDescription, isDescending),
            "trainingcategory" => ApplySort(rows, row => row.TrainingCategory, isDescending),
            "awardedon" => ApplySort(rows, row => row.AwardedOn, isDescending),
            "endingon" => ApplySort(rows, row => row.EndingOn, isDescending),
            "expirydate" => ApplySort(rows, row => row.ExpiryDate, isDescending),
            "version" => ApplySort(rows, row => row.Version, isDescending),
            "noticestate" => ApplySort(rows, row => row.NoticeState, isDescending),
            _ => ApplyDefaultSort(rows),
        };
    }

    private static List<UserTrainingReportRow> ApplyNameSort(IEnumerable<UserTrainingReportRow> rows, bool isDescending)
    {
        return (
            isDescending
                ? rows.OrderByDescending(row => row.LastName).ThenByDescending(row => row.FirstName)
                : rows.OrderBy(row => row.LastName).ThenBy(row => row.FirstName)
        ).ToList();
    }

    private static List<UserTrainingReportRow> ApplySort<TKey>(
        IEnumerable<UserTrainingReportRow> rows,
        Func<UserTrainingReportRow, TKey> keySelector,
        bool isDescending
    )
    {
        return (isDescending ? rows.OrderByDescending(keySelector) : rows.OrderBy(keySelector)).ToList();
    }

    private static List<UserTrainingReportRow> ApplyDefaultSort(IEnumerable<UserTrainingReportRow> rows)
    {
        return
        [
            .. rows.OrderBy(row => row.LastName)
                .ThenBy(row => row.FirstName)
                .ThenBy(row => row.TrainingCode)
                .ThenByDescending(row => row.AwardedOn),
        ];
    }

    private static string NormalizeSortBy(string? sortBy)
    {
        return string.IsNullOrWhiteSpace(sortBy) ? string.Empty : sortBy.Trim().ToLowerInvariant();
    }

    private static UserTrainingReportQuery ParseQuery(IReadOnlyDictionary<string, IReadOnlyCollection<string>> filters)
    {
        var userId = ParseOptional<Guid>(filters, UserIdFilterKey, Guid.TryParse, "must be a valid GUID");
        var trainingId = ParseOptional<int>(
            filters,
            TrainingIdFilterKey,
            TryParsePositiveInt,
            "must be a positive integer"
        );
        var trainingCode = ParseStringFilter(filters, TrainingCodeFilterKey);
        var status = ParseStatusFilter(filters);
        var startDate = ParseOptional<DateOnly>(
            filters,
            StartDateFilterKey,
            DateOnly.TryParse,
            "must be a valid date in YYYY-MM-DD format"
        );
        var endDate = ParseOptional<DateOnly>(
            filters,
            EndDateFilterKey,
            DateOnly.TryParse,
            "must be a valid date in YYYY-MM-DD format"
        );

        if (startDate.HasValue && endDate.HasValue && startDate > endDate)
        {
            throw new ArgumentException("Filter 'startDate' must be on or before 'endDate'.");
        }

        return new UserTrainingReportQuery(userId, trainingId, trainingCode, status, startDate, endDate);
    }

    private static string GetStatus(UserTrainingReportRow row, DateTimeOffset now)
    {
        if (row.IsMissingMandatoryTrainingAssignment)
        {
            return "Not Taken";
        }

        return row.ExpiryDate == null || row.ExpiryDate > now ? "Active" : "Expired";
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

    private readonly record struct UserTrainingReportQuery(
        Guid? UserId,
        int? TrainingId,
        string? TrainingCode,
        TrainingCompletionStatus? Status,
        DateOnly? StartDate,
        DateOnly? EndDate
    )
    {
        public string? NormalizedTrainingCode => NormalizeForContains(TrainingCode);

        public DateTimeOffset? StartDateInclusive =>
            StartDate.HasValue
                ? new DateTimeOffset(StartDate.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
                : null;

        public DateTimeOffset? EndDateExclusive =>
            EndDate.HasValue
                ? new DateTimeOffset(EndDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
                : null;

        public bool ShouldIncludeMissingMandatoryRows => Status is null && !StartDate.HasValue && !EndDate.HasValue;
    }
}
