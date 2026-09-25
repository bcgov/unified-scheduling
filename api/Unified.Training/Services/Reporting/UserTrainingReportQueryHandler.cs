using Microsoft.EntityFrameworkCore;
using Unified.Common.Reporting;
using Unified.Db;
using Unified.Training.Mappings;

namespace Unified.Training.Services.Reporting;

public sealed class UserTrainingReportQueryHandler(UnifiedDbContext db, TimeProvider timeProvider) : IReportQueryHandler
{
    public string ReportKey => "user-training";

    public async Task<PagedResponse> ExecuteAsync(
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> filters,
        string? sortBy,
        SortDirection sortDirection,
        CancellationToken cancellationToken = default
    )
    {
        var now = timeProvider.GetUtcNow();
        var queryFilters = UserTrainingReportQueryParser.Parse(filters);
        var reportRowsQuery = BuildReportRowsQuery(queryFilters, now);
        var sortedRowsQuery = UserTrainingReportRowSorter.Apply(reportRowsQuery, sortBy, sortDirection);

        var pageRows = await sortedRowsQuery.ToListAsync(cancellationToken);
        var totalRows = pageRows.Count;

        var rows = pageRows
            .Select(row => UserTrainingReportMappings.ToReportRowValue(row, ResolveStatus(row, now)))
            .ToArray();

        return new UserTrainingReportResponse(rows, totalRows);
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
            .Where(ut => ut.User.IsEnabled)
            .Where(ut => ut.Training.ExpiryDate == null || ut.Training.ExpiryDate > now);

        query = queryFilters.UserIds is { Count: > 0 } userIds ? query.Where(ut => userIds.Contains(ut.UserId)) : query;

        query = queryFilters.RegionIds is { Count: > 0 } regionIds
            ? query.Where(ut =>
                ut.User.HomeLocation != null
                && ut.User.HomeLocation.RegionId != null
                && regionIds.Contains(ut.User.HomeLocation.RegionId.Value)
            )
            : query;

        query = queryFilters.LocationIds is { Count: > 0 } locationIds
            ? query.Where(ut => ut.User.HomeLocationId != null && locationIds.Contains(ut.User.HomeLocationId.Value))
            : query;

        query = queryFilters.TrainingIds is { Count: > 0 } trainingIds
            ? query.Where(ut => trainingIds.Contains(ut.TrainingId))
            : query;

        query = queryFilters.TrainingCodes is { Count: > 0 } trainingCodes
            ? query.Where(ut => trainingCodes.Contains(ut.Training.Code))
            : query;

        var startDateValue = queryFilters.StartDate is DateOnly startDate
            ? new DateTimeOffset(startDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
            : (DateTimeOffset?)null;
        query = startDateValue is DateTimeOffset parsedStartDateValue
            ? query.Where(ut => ut.AwardedOn >= parsedStartDateValue)
            : query;

        var endDateValue = queryFilters.EndDate is DateOnly endDate
            ? new DateTimeOffset(endDate.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
            : (DateTimeOffset?)null;
        query = endDateValue is DateTimeOffset parsedEndDateValue
            ? query.Where(ut => ut.AwardedOn < parsedEndDateValue)
            : query;

        if (!queryFilters.ShouldIncludeActiveRows && !queryFilters.ShouldIncludeExpiredRows)
        {
            query = query.Where(_ => false);
        }
        else if (!queryFilters.ShouldIncludeExpiredRows)
        {
            query = query.Where(ut => ut.ExpiryDate == null || ut.ExpiryDate > now);
        }
        else if (!queryFilters.ShouldIncludeActiveRows)
        {
            query = query.Where(ut => ut.ExpiryDate != null && ut.ExpiryDate <= now);
        }

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
            RegionName =
                ut.User.HomeLocation != null && ut.User.HomeLocation.Region != null
                    ? ut.User.HomeLocation.Region.Name
                    : null,
            LocationName = ut.User.HomeLocation != null ? ut.User.HomeLocation.Name : null,
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
        usersQuery = queryFilters.UserIds is { Count: > 0 } reportUserIds
            ? usersQuery.Where(user => reportUserIds.Contains(user.Id))
            : usersQuery;

        usersQuery = queryFilters.RegionIds is { Count: > 0 } reportRegionIds
            ? usersQuery.Where(user =>
                user.HomeLocation != null
                && user.HomeLocation.RegionId != null
                && reportRegionIds.Contains(user.HomeLocation.RegionId.Value)
            )
            : usersQuery;

        usersQuery = queryFilters.LocationIds is { Count: > 0 } reportLocationIds
            ? usersQuery.Where(user =>
                user.HomeLocationId != null && reportLocationIds.Contains(user.HomeLocationId.Value)
            )
            : usersQuery;

        var mandatoryTrainingsQuery = db
            .Trainings.AsNoTracking()
            .Where(training => training.Mandatory && (training.ExpiryDate == null || training.ExpiryDate > now));

        mandatoryTrainingsQuery = queryFilters.TrainingIds is { Count: > 0 } reportTrainingIds
            ? mandatoryTrainingsQuery.Where(training => reportTrainingIds.Contains(training.Id))
            : mandatoryTrainingsQuery;

        mandatoryTrainingsQuery = queryFilters.TrainingCodes is { Count: > 0 } mandatoryTrainingCodes
            ? mandatoryTrainingsQuery.Where(training => mandatoryTrainingCodes.Contains(training.Code))
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
                RegionName =
                    user.HomeLocation != null && user.HomeLocation.Region != null
                        ? user.HomeLocation.Region.Name
                        : null,
                LocationName = user.HomeLocation != null ? user.HomeLocation.Name : null,
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

    private static TrainingComplianceStatus ResolveStatus(UserTrainingReportRow row, DateTimeOffset now)
    {
        if (row.IsMissingMandatoryTrainingAssignment)
        {
            return TrainingComplianceStatus.NotTaken;
        }

        return row.ExpiryDate == null || row.ExpiryDate > now
            ? TrainingComplianceStatus.Active
            : TrainingComplianceStatus.Expired;
    }
}
