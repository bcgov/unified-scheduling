namespace Unified.DataMigration.Sources;

public sealed record SsSchemaPreflightResult(IReadOnlyList<string> CheckedTables);

public sealed record SsRegionDto(
    int Id,
    int? JustinId,
    string? Code,
    string? Name,
    DateTimeOffset? ExpiryDate,
    DateTimeOffset CreatedOn,
    DateTimeOffset? UpdatedOn
);

public sealed record SsLocationDto(
    int Id,
    string AgencyId,
    string? Name,
    string? JustinCode,
    int? ParentLocationId,
    int? RegionId,
    string? Timezone,
    DateTimeOffset? ExpiryDate,
    DateTimeOffset CreatedOn,
    DateTimeOffset? UpdatedOn
);

public sealed record SsLookupCodeDto(
    int Id,
    int Type,
    string? Code,
    string? SubCode,
    string? Description,
    DateTimeOffset? EffectiveDate,
    DateTimeOffset? ExpiryDate,
    int? LocationId,
    bool Mandatory,
    int ValidityPeriod,
    string? Category,
    int AdvanceNotice,
    bool Rotating,
    DateTimeOffset CreatedOn,
    DateTimeOffset? UpdatedOn
);

public sealed record SsLookupSortOrderDto(
    int Id,
    int LookupCodeId,
    int? LocationId,
    int SortOrder,
    DateTimeOffset CreatedOn,
    DateTimeOffset? UpdatedOn
);
