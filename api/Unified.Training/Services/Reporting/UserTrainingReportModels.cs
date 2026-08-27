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

public sealed record UserTrainingReportResponse(
    IReadOnlyCollection<UserTrainingReportItem> Rows,
    int Page,
    int PageSize,
    int TotalRows
) : PagedResponse(Page, PageSize, TotalRows);
