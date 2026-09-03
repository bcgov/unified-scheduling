using Unified.Common.Reporting;

namespace Unified.Training.Services.Reporting;

internal sealed record UserTrainingReportRow
{
    public required Guid UserId { get; init; }

    public required string FirstName { get; init; }

    public required string LastName { get; init; }

    public required int TrainingId { get; init; }

    public required string TrainingCode { get; init; }

    public required string TrainingDescription { get; init; }

    public DateTimeOffset? AwardedOn { get; init; }

    public DateTimeOffset? EndingOn { get; init; }

    public DateTimeOffset? ExpiryDate { get; init; }

    public int? Version { get; init; }

    public required string NoticeState { get; init; }

    public string? Notes { get; init; }

    public required bool IsMissingMandatoryTrainingAssignment { get; init; }
}

internal enum TrainingComplianceStatus
{
    Active,
    Expired,
    NotTaken,
}

internal static class TrainingComplianceStatusExtensions
{
    public static string ToDisplayValue(this TrainingComplianceStatus status) =>
        status switch
        {
            TrainingComplianceStatus.Active => "Active",
            TrainingComplianceStatus.Expired => "Expired",
            TrainingComplianceStatus.NotTaken => "Not Taken",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
        };
}

public sealed record UserTrainingReportItem(
    string UserDisplayName,
    int TrainingId,
    string TrainingCode,
    string TrainingDescription,
    DateTimeOffset? AwardedOn,
    DateTimeOffset? EndingOn,
    DateTimeOffset? ExpiryDate,
    string Status,
    int? Version,
    string NoticeState,
    string? Notes,
    bool HasMissingMandatoryTrainingAssignment
);

public sealed record UserTrainingReportResponse(IReadOnlyCollection<UserTrainingReportItem> Rows, int TotalRows)
    : PagedResponse(TotalRows);
