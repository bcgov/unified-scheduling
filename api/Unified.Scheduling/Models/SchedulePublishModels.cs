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

public sealed record SchedulePublishEmployeeWarningResponse(Guid UserId, string DisplayName);

public sealed record SchedulePublishEventWarningResponse(int EventId, string Title);

public sealed record SchedulePublishWarningsResponse(
    IReadOnlyCollection<SchedulePublishEmployeeWarningResponse> IncompleteEmployees,
    IReadOnlyCollection<SchedulePublishEventWarningResponse> UnassignedShifts,
    IReadOnlyCollection<SchedulePublishEventWarningResponse> UnassignedAssignments
);

public sealed record SchedulePublishConflictResponse(
    string Id,
    string FirstSourceModule,
    string FirstEventId,
    string SecondSourceModule,
    string SecondEventId,
    Guid ResourceId,
    DateTimeOffset OverlapStart,
    DateTimeOffset OverlapEnd
);

public sealed record SchedulePublishShiftBlockerResponse(string Message);

public sealed record SchedulePublishBlockersResponse(
    IReadOnlyCollection<SchedulePublishConflictResponse> Conflicts,
    IReadOnlyCollection<SchedulePublishShiftBlockerResponse> ShiftConflicts
);

public sealed record SchedulePublishPreviewResponse(
    SchedulePublishScopeResponse Scope,
    SchedulePublishCandidateCountsResponse Candidates,
    SchedulePublishWarningsResponse Warnings,
    SchedulePublishBlockersResponse Blockers,
    bool CanPublish
);

public sealed record SchedulePublishResult(int PublishedShiftEntryCount, int PublishedAssignmentEntryCount);
