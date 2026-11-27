using Unified.Calendar.Models;

namespace Unified.Scheduling.Models;

public sealed record SchedulePublishRequest(int LocationId, DateOnly StartDate, DateOnly EndDate);

public sealed record SchedulePublishScopeResponse(
    int LocationId,
    DateOnly StartDate,
    DateOnly EndDate,
    string TimeZoneId,
    DateTimeOffset RangeStartUtc,
    DateTimeOffset RangeEndExclusiveUtc
);

public sealed record SchedulePublishCandidateCountsResponse(int ShiftEntryCount, int AssignmentEntryCount)
{
    public int TotalCount => ShiftEntryCount + AssignmentEntryCount;
}

public sealed record SchedulePublishEmployeeResponse(Guid UserId, string DisplayName);

public sealed record SchedulePublishEmployeeWarningResponse(Guid UserId, string DisplayName)
{
    public bool MissingShift { get; init; }
    public bool MissingAssignment { get; init; }
}

public sealed record SchedulePublishEventWarningResponse(int EventId, string Title);

public sealed record SchedulePublishWarningsResponse(
    IReadOnlyCollection<SchedulePublishEmployeeWarningResponse> IncompleteEmployees,
    IReadOnlyCollection<SchedulePublishEventWarningResponse> UnassignedShifts,
    IReadOnlyCollection<SchedulePublishEventWarningResponse> UnassignedAssignments
)
{
    public IReadOnlyCollection<SchedulePublishConflictResponse> Conflicts { get; init; } = [];
    public IReadOnlyCollection<CalendarRestrictedConflictWarningResponse> RestrictedWarnings { get; init; } = [];
    public bool HasRestrictedConflicts { get; init; }
    public bool HasUnattributedRestrictedConflicts { get; init; }
}

public sealed record SchedulePublishConflictResponse(
    string Id,
    string FirstSourceModule,
    string FirstEventId,
    string SecondSourceModule,
    string SecondEventId,
    Guid ResourceId,
    DateTimeOffset OverlapStart,
    DateTimeOffset OverlapEnd
)
{
    public string? Title { get; init; }
    public CalendarConflictEventResponse? Entry { get; init; }
    public CalendarConflictEventResponse? Overlaps { get; init; }
}

public enum ShiftConflictKind
{
    StartDate,
    Overlap,
}

public sealed record SchedulePublishShiftEventResponse(
    int ShiftEntryId,
    int EventId,
    string Title,
    DateTimeOffset StartAtUtc,
    DateTimeOffset? EndAtUtc
);

public sealed record SchedulePublishShiftBlockerResponse(string Message)
{
    public Guid? UserId { get; init; }
    public ShiftConflictKind? Kind { get; init; }
    public SchedulePublishShiftEventResponse? FirstShift { get; init; }
    public SchedulePublishShiftEventResponse? SecondShift { get; init; }
}

public sealed record SchedulePublishBlockersResponse(
    IReadOnlyCollection<SchedulePublishConflictResponse> Conflicts,
    IReadOnlyCollection<SchedulePublishShiftBlockerResponse> ShiftConflicts
)
{
    public IReadOnlyCollection<CalendarRestrictedConflictWarningResponse> RestrictedWarnings { get; init; } = [];
    public bool HasRestrictedConflicts { get; init; }
    public bool HasUnattributedRestrictedConflicts { get; init; }
}

public sealed record SchedulePublishPreviewResponse(
    SchedulePublishScopeResponse Scope,
    SchedulePublishCandidateCountsResponse Candidates,
    SchedulePublishWarningsResponse Warnings,
    SchedulePublishBlockersResponse Blockers,
    bool CanPublish
)
{
    public IReadOnlyCollection<SchedulePublishEmployeeResponse> AffectedEmployees { get; init; } = [];
}

public sealed record SchedulePublishResult(int PublishedShiftEntryCount, int PublishedAssignmentEntryCount);
