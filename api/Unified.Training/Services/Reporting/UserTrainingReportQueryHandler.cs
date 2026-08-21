using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Unified.Core.Models.Reporting;
using Unified.Core.Services.Reporting;
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

    public static string ReportKey => "user-training";

    private static IReadOnlyCollection<ReportColumn> Columns =>
    [
        new("userDisplayName", "User", ReportValueType.String),
        new("trainingId", "ID", ReportValueType.Number),
        new("trainingCode", "Code", ReportValueType.String),
        new("trainingDescription", "Description", ReportValueType.String),
        new("trainingCategory", "Category", ReportValueType.String),
        new("awardedOn", "Awarded On", ReportValueType.DateTime),
        new("endingOn", "Ending On", ReportValueType.DateTime),
        new("expiryDate", "Expiry Date", ReportValueType.DateTime),
        new("status", "Status", ReportValueType.String, Sortable: false),
        new("version", "Version", ReportValueType.Number),
        new("noticeState", "Notice State", ReportValueType.String),
        new("notes", "Notes", ReportValueType.String, Sortable: false),
    ];

    public async Task<ReportQueryResult> ExecuteAsync(
        ReportQueryRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        var now = DateTimeOffset.UtcNow;
        var filters = ParseFilters(request);

        var query = db
            .UserTrainings.AsNoTracking()
            .Where(ut => ut.Training.ExpiryDate == null || ut.Training.ExpiryDate > now);

        query = filters.UserId is Guid userId
            ? query.Where(ut => ut.UserId == userId)
            : query;

        query = filters.TrainingId is int trainingId
            ? query.Where(ut => ut.TrainingId == trainingId)
            : query;

        query = filters.NormalizedTrainingCode is string trainingCode
            ? query.Where(ut => ut.Training.Code.ToLower().Contains(trainingCode))
            : query;

        query = filters.StartDateInclusive is DateTimeOffset startDateInclusive
            ? query.Where(ut => ut.AwardedOn >= startDateInclusive)
            : query;

        query = filters.EndDateExclusive is DateTimeOffset endDateExclusive
            ? query.Where(ut => ut.AwardedOn < endDateExclusive)
            : query;

        query = filters.Status switch
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

        if (filters.ShouldIncludeMissingMandatoryRows)
        {
            var usersQuery = db.Users.AsNoTracking().Where(user => user.IsEnabled);
            usersQuery = filters.UserId is Guid reportUserId
                ? usersQuery.Where(user => user.Id == reportUserId)
                : usersQuery;

            var mandatoryTrainingsQuery = db
                .Trainings.AsNoTracking()
                .Where(training =>
                    training.Mandatory && (training.ExpiryDate == null || training.ExpiryDate > now)
                );

            mandatoryTrainingsQuery = filters.TrainingId is int reportTrainingId
                ? mandatoryTrainingsQuery.Where(training => training.Id == reportTrainingId)
                : mandatoryTrainingsQuery;

            mandatoryTrainingsQuery = filters.NormalizedTrainingCode is string mandatoryTrainingCode
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

        var sortedRows = UserTrainingReportSorting.Apply(reportRows, request.SortBy, request.SortDirection);
        var totalRows = sortedRows.Count;
        var pageRows = sortedRows
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        var rows = pageRows
            .Select(row =>
                (IReadOnlyDictionary<string, object?>)
                    UserTrainingReportMappings.ToReportRowDictionary(row, GetStatus(row, now))
            )
            .ToArray();

        var executionMs = (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;

        return new ReportQueryResult(
            ReportKey,
            Columns,
            rows,
            request.Page,
            request.PageSize,
            totalRows,
            executionMs
        );
    }

    private static UserTrainingReportFilters ParseFilters(ReportQueryRequest request)
    {
        var userId = ParseGuidFilter(request, UserIdFilterKey);
        var trainingId = ParseIntFilter(request, TrainingIdFilterKey);
        var normalizedTrainingCode = NormalizeForContains(ParseStringFilter(request, TrainingCodeFilterKey));
        var status = ParseStatusFilter(request);
        var startDate = ParseDateFilter(request, StartDateFilterKey);
        var endDate = ParseDateFilter(request, EndDateFilterKey);

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

    private static string? ParseStringFilter(ReportQueryRequest request, string filterKey)
    {
        if (!request.Filters.TryGetValue(filterKey, out var values))
        {
            return null;
        }

        var value = values.FirstOrDefault(entry => !string.IsNullOrWhiteSpace(entry));
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static Guid? ParseGuidFilter(ReportQueryRequest request, string filterKey)
    {
        return ParseFilter<Guid>(
            request,
            filterKey,
            Guid.TryParse,
            $"Filter '{filterKey}' must be a valid GUID."
        );
    }

    private static int? ParseIntFilter(ReportQueryRequest request, string filterKey)
    {
        return ParseFilter<int>(
            request,
            filterKey,
            TryParsePositiveInt,
            $"Filter '{filterKey}' must be a positive integer."
        );
    }

    private static DateOnly? ParseDateFilter(ReportQueryRequest request, string filterKey)
    {
        return ParseFilter<DateOnly>(
            request,
            filterKey,
            DateOnly.TryParse,
            $"Filter '{filterKey}' must be a valid date in YYYY-MM-DD format."
        );
    }

    private static T? ParseFilter<T>(
        ReportQueryRequest request,
        string filterKey,
        TryParseFilterValue<T> tryParse,
        string errorMessage
    )
        where T : struct
    {
        var rawValue = ParseStringFilter(request, filterKey);
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

    private static TrainingCompletionStatus? ParseStatusFilter(ReportQueryRequest request)
    {
        var rawStatus = ParseStringFilter(request, StatusFilterKey);
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

    private delegate bool TryParseFilterValue<T>(string rawValue, out T parsed);
}
