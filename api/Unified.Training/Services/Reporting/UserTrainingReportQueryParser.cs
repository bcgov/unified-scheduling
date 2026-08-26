using Unified.Common.Reporting;

namespace Unified.Training.Services.Reporting;

internal sealed class UserTrainingReportQueryParser : ReportQueryHandlerBase
{
    private const string UserIdFilterKey = "userId";
    private const string TrainingIdFilterKey = "trainingId";
    private const string TrainingCodeFilterKey = "trainingCode";
    private const string StatusFilterKey = "status";
    private const string StartDateFilterKey = "startDate";
    private const string EndDateFilterKey = "endDate";

    public static UserTrainingReportQuery Parse(IReadOnlyDictionary<string, IReadOnlyCollection<string>> filters)
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
}

internal enum TrainingCompletionStatus
{
    Active,
    Expired,
}

internal readonly record struct UserTrainingReportQuery(
    Guid? UserId,
    int? TrainingId,
    string? TrainingCode,
    TrainingCompletionStatus? Status,
    DateOnly? StartDate,
    DateOnly? EndDate
)
{
    public string? NormalizedTrainingCode =>
        string.IsNullOrWhiteSpace(TrainingCode) ? null : TrainingCode.Trim().ToLowerInvariant();

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
