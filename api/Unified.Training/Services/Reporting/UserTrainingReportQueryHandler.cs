using Microsoft.EntityFrameworkCore;
using Unified.Common.Reporting;
using Unified.Db;
using Unified.Training.Mappings;

namespace Unified.Training.Services.Reporting;

public sealed class UserTrainingReportQueryHandler(UnifiedDbContext db) : IReportQueryHandler
{
    public string ReportKey => "user-training";

    public async Task<PagedResponse> ExecuteAsync(
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
        var queryFilters = UserTrainingReportQueryParser.Parse(filters);
        var reportRows = await QueryReportRowsAsync(queryFilters, now, cancellationToken);

        var sortedRows = UserTrainingReportRowSorter.Apply(reportRows, sortBy, sortDirection);
        var totalRows = sortedRows.Count;
        var pageRows = sortedRows.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var rows = pageRows
            .Select(row => UserTrainingReportMappings.ToReportRowValue(row, ResolveStatus(row, now)))
            .ToArray();

        return new UserTrainingReportResponse(rows, page, pageSize, totalRows);
    }

    private async Task<List<UserTrainingReportRow>> QueryReportRowsAsync(
        UserTrainingReportQuery queryFilters,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        var reportRows = await QueryAssignedTrainingRowsAsync(queryFilters, now, cancellationToken);

        if (queryFilters.ShouldIncludeMissingMandatoryRows)
        {
            var missingMandatoryRows = await QueryMissingMandatoryRowsAsync(queryFilters, now, cancellationToken);
            reportRows.AddRange(missingMandatoryRows);
        }

        return reportRows;
    }

    private async Task<List<UserTrainingReportRow>> QueryAssignedTrainingRowsAsync(
        UserTrainingReportQuery queryFilters,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        var query = db
            .UserTrainings.AsNoTracking()
            .Where(ut => ut.Training.ExpiryDate == null || ut.Training.ExpiryDate > now);

        query = queryFilters.UserId is Guid userId ? query.Where(ut => ut.UserId == userId) : query;

        query = queryFilters.TrainingId is int trainingId ? query.Where(ut => ut.TrainingId == trainingId) : query;

        query = queryFilters.TrainingCode is string trainingCode
            ? query.Where(ut => ut.Training.Code.Contains(trainingCode))
            : query;

        var startDateValue = queryFilters.StartDate is DateOnly startDate
            ? new DateTimeOffset(startDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
            : (DateTimeOffset?)null;
        query = startDateValue is DateTimeOffset parsedStartDateValue
            ? query.Where(ut => ut.AwardedOn >= parsedStartDateValue)
            : query;

        var endDateValue = queryFilters.EndDate is DateOnly endDate
            ? new DateTimeOffset(endDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
            : (DateTimeOffset?)null;
        query = endDateValue is DateTimeOffset parsedEndDateValue
            ? query.Where(ut => ut.AwardedOn <= parsedEndDateValue)
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

        return await query
            .Select(ut => new UserTrainingReportRow(
                ut.UserId,
                ut.User.FirstName,
                ut.User.LastName,
                ut.TrainingId,
                ut.Training.Code,
                ut.Training.Description,
                ut.AwardedOn,
                ut.EndingOn,
                ut.ExpiryDate,
                ut.Version,
                ut.NoticeState,
                ut.Notes,
                false
            ))
            .ToListAsync(cancellationToken);
    }

    private async Task<List<UserTrainingReportRow>> QueryMissingMandatoryRowsAsync(
        UserTrainingReportQuery queryFilters,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
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

        mandatoryTrainingsQuery = queryFilters.TrainingCode is string mandatoryTrainingCode
            ? mandatoryTrainingsQuery.Where(training => training.Code.Contains(mandatoryTrainingCode))
            : mandatoryTrainingsQuery;

        return await (
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
                null,
                null,
                null,
                null,
                string.Empty,
                string.Empty,
                true
            )
        ).ToListAsync(cancellationToken);
    }

    private static string ResolveStatus(UserTrainingReportRow row, DateTimeOffset now)
    {
        if (row.IsMissingMandatoryTrainingAssignment)
        {
            return "Not Taken";
        }

        return row.ExpiryDate == null || row.ExpiryDate > now ? "Active" : "Expired";
    }
}
