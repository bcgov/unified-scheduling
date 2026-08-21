namespace Unified.Training.Services.Reporting;

internal sealed record UserTrainingReportRow(
    Guid UserId,
    string FirstName,
    string LastName,
    int TrainingId,
    string TrainingCode,
    string TrainingDescription,
    string TrainingCategory,
    DateTimeOffset? AwardedOn,
    DateTimeOffset? EndingOn,
    DateTimeOffset? ExpiryDate,
    int? Version,
    string NoticeState,
    string? Notes,
    bool IsMissingMandatoryTrainingAssignment
);

internal sealed record UserTrainingReportRowValue(
    string UserDisplayName,
    int TrainingId,
    string TrainingCode,
    string TrainingDescription,
    string TrainingCategory,
    DateTimeOffset? AwardedOn,
    DateTimeOffset? EndingOn,
    DateTimeOffset? ExpiryDate,
    string Status,
    int? Version,
    string NoticeState,
    string? Notes,
    bool HasMissingMandatoryTrainingAssignment
);
