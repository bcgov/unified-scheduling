using Unified.Common.Reporting;

namespace Unified.Training.Services.Reporting;

internal sealed class UserTrainingReportQueryParser : ReportQueryHandlerBase
{
    private const string UserIdFilterKey = "userId";
    private const string RegionIdFilterKey = "regionId";
    private const string LocationIdFilterKey = "locationId";
    private const string TrainingIdFilterKey = "trainingId";
    private const string TrainingCodeFilterKey = "trainingCode";
    private const string StatusFilterKey = "status";
    private const string StartDateFilterKey = "startDate";
    private const string EndDateFilterKey = "endDate";

    public static UserTrainingReportQuery Parse(IReadOnlyDictionary<string, IReadOnlyCollection<string>> filters)
    {
        var userIds = ParseFilters<Guid>(filters, UserIdFilterKey, Guid.TryParse, "must be valid GUIDs");
        var regionIds = ParseFilters<int>(filters, RegionIdFilterKey, int.TryParse, "must be valid integers");
        var locationIds = ParseFilters<int>(filters, LocationIdFilterKey, int.TryParse, "must be valid integers");
        var trainingIds = ParseFilters<int>(filters, TrainingIdFilterKey, int.TryParse, "must be valid integers");

        var trainingCodes = ParseStringFilters(filters, TrainingCodeFilterKey);
        var statuses = ParseStatusFilters(filters);
        var startDate = ParseFilter<DateOnly>(filters, StartDateFilterKey, DateOnly.TryParse, "must be a valid date");
        var endDate = ParseFilter<DateOnly>(filters, EndDateFilterKey, DateOnly.TryParse, "must be a valid date");

        if (startDate.HasValue && endDate.HasValue && startDate > endDate)
        {
            throw new ArgumentException("Filter 'startDate' must be on or before 'endDate'.");
        }

        return new UserTrainingReportQuery(
            userIds,
            regionIds,
            locationIds,
            trainingIds,
            trainingCodes,
            statuses,
            startDate,
            endDate
        );
    }

    private static IReadOnlyCollection<TrainingCompletionStatus>? ParseStatusFilters(
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> filters
    )
    {
        var rawStatuses = ParseStringFilters(filters, StatusFilterKey);
        if (rawStatuses is null)
        {
            return null;
        }

        var statuses = rawStatuses
            .Select(rawStatus =>
                rawStatus.Trim().ToLowerInvariant() switch
                {
                    "active" => TrainingCompletionStatus.Active,
                    "expired" => TrainingCompletionStatus.Expired,
                    "nottaken" => TrainingCompletionStatus.NotTaken,
                    _ => throw new ArgumentException(
                        "Filter 'status' must be one of 'active', 'expired', or 'notTaken'."
                    ),
                }
            )
            .Distinct()
            .ToArray();

        return statuses.Length == 0 ? null : statuses;
    }

    private static IReadOnlyCollection<string>? ParseStringFilters(
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> filters,
        string filterKey
    )
    {
        if (!filters.TryGetValue(filterKey, out var values))
        {
            return null;
        }

        var parsedValues = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return parsedValues.Length == 0 ? null : parsedValues;
    }

    private static IReadOnlyCollection<T>? ParseFilters<T>(
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> filters,
        string filterKey,
        TryParseFilterValue<T> tryParse,
        string formatMessage
    )
        where T : struct
    {
        var rawValues = ParseStringFilters(filters, filterKey);
        if (rawValues is null)
        {
            return null;
        }

        var parsedValues = new HashSet<T>();
        foreach (var rawValue in rawValues)
        {
            if (!tryParse(rawValue, out var parsedValue))
            {
                throw new ArgumentException($"Filter '{filterKey}' {formatMessage}.");
            }

            parsedValues.Add(parsedValue);
        }

        return parsedValues.Count == 0 ? null : parsedValues.ToArray();
    }
}

internal enum TrainingCompletionStatus
{
    Active,
    Expired,
    NotTaken,
}

internal readonly record struct UserTrainingReportQuery(
    IReadOnlyCollection<Guid>? UserIds,
    IReadOnlyCollection<int>? RegionIds,
    IReadOnlyCollection<int>? LocationIds,
    IReadOnlyCollection<int>? TrainingIds,
    IReadOnlyCollection<string>? TrainingCodes,
    IReadOnlyCollection<TrainingCompletionStatus>? Statuses,
    DateOnly? StartDate,
    DateOnly? EndDate
)
{
    public bool ShouldIncludeMissingMandatoryRows =>
        Statuses switch
        {
            not null when Statuses.Contains(TrainingCompletionStatus.NotTaken) => true,
            null => !StartDate.HasValue && !EndDate.HasValue,
            _ => false,
        };

    public bool ShouldIncludeActiveRows => Statuses is null || Statuses.Contains(TrainingCompletionStatus.Active);

    public bool ShouldIncludeExpiredRows => Statuses is null || Statuses.Contains(TrainingCompletionStatus.Expired);
}
