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
        var reportRowsQuery = BuildReportRowsQuery(queryFilters, now);
        var sortedRowsQuery = UserTrainingReportRowSorter.Apply(reportRowsQuery, sortBy, sortDirection);

        var totalRows = await sortedRowsQuery.CountAsync(cancellationToken);
        var pageRows = await sortedRowsQuery.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        var rows = pageRows
            .Select(row => UserTrainingReportMappings.ToReportRowValue(row, ResolveStatus(row, now)))
            .ToArray();

        return new UserTrainingReportResponse(rows, page, pageSize, totalRows);
    }

    private IQueryable<UserTrainingReportRow> BuildReportRowsQuery(
        UserTrainingReportQuery queryFilters,
        DateTimeOffset now
    )
    {
        var reportRowsQuery = BuildAssignedTrainingRowsQuery(queryFilters, now);

        if (queryFilters.ShouldIncludeMissingMandatoryRows)
        {
            reportRowsQuery = reportRowsQuery.Concat(BuildMissingMandatoryRowsQuery(queryFilters, now));
        }

        return reportRowsQuery;
    }

    private IQueryable<UserTrainingReportRow> BuildAssignedTrainingRowsQuery(
        UserTrainingReportQuery queryFilters,
        DateTimeOffset now
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

        return query.Select(ut => new UserTrainingReportRow
        {
            UserId = ut.UserId,
            FirstName = ut.User.FirstName,
            LastName = ut.User.LastName,
            TrainingId = ut.TrainingId,
            TrainingCode = ut.Training.Code,
            TrainingDescription = ut.Training.Description,
            AwardedOn = ut.AwardedOn,
            EndingOn = ut.EndingOn,
            ExpiryDate = ut.ExpiryDate,
            Version = ut.Version,
            NoticeState = ut.NoticeState,
            Notes = ut.Notes,
            IsMissingMandatoryTrainingAssignment = false,
        });
    }

    private IQueryable<UserTrainingReportRow> BuildMissingMandatoryRowsQuery(
        UserTrainingReportQuery queryFilters,
        DateTimeOffset now
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

        return (
            from user in usersQuery
            from training in mandatoryTrainingsQuery
            where !db.UserTrainings.Any(ut => ut.UserId == user.Id && ut.TrainingId == training.Id)
            select new UserTrainingReportRow
            {
                UserId = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                TrainingId = training.Id,
                TrainingCode = training.Code,
                TrainingDescription = training.Description,
                AwardedOn = null,
                EndingOn = null,
                ExpiryDate = null,
                Version = null,
                NoticeState = string.Empty,
                Notes = string.Empty,
                IsMissingMandatoryTrainingAssignment = true,
            }
        );
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
