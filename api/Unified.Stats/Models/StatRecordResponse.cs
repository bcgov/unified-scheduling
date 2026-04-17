namespace Unified.Stats.Models;

public sealed record StatRecordResponse(
    int Id,
    DateOnly DateFrom,
    DateOnly DateTo,
    string PeriodType,
    int LocationId,
    int SubCategoryMetricId,
    decimal Value,
    string? Comment,
    string Status,
    DateTimeOffset CreatedOn,
    Guid? CreatedById,
    DateTimeOffset? UpdatedOn
);
