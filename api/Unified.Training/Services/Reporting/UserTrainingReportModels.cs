using Unified.Common.Reporting;

namespace Unified.Training.Services.Reporting;

internal sealed record UserTrainingReportRow(
    Guid UserId,
    string FirstName,
    string LastName,
    int TrainingId,
    string TrainingCode,
    string TrainingDescription,
    DateTimeOffset? AwardedOn,
    DateTimeOffset? EndingOn,
    DateTimeOffset? ExpiryDate,
    int? Version,
    string NoticeState,
    string? Notes,
    bool IsMissingMandatoryTrainingAssignment
);

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
