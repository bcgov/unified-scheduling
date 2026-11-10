using Unified.Calendar.Conflicts;
using Unified.Common.Calendar.Conflicts;

namespace Unified.Calendar.Models;

public sealed record CalendarConflictEventResponse(
    string EventId,
    string SourceModule,
    string Title,
    DateTimeOffset Start,
    DateTimeOffset End,
    int? SourceEntityId,
    string? TimeZoneId,
    string? CanonicalEventId = null,
    bool AllDay = false
);

public sealed record CalendarConflictResponse(
    string Id,
    CalendarConflictEventResponse Entry,
    CalendarConflictEventResponse Overlaps,
    Guid ResourceId,
    DateTimeOffset OverlapStart,
    DateTimeOffset OverlapEnd,
    bool IsOverridden,
    string? OverrideNote,
    Guid? CreatedById,
    DateTimeOffset? CreatedOn,
    Guid? UpdatedById,
    DateTimeOffset? UpdatedOn,
    bool ReportOnEntry,
    bool ReportOnOverlaps,
    string Title
)
{
    public static CalendarConflictResponse FromConflict(CalendarConflict conflict) =>
        new(
            conflict.Id,
            MapEvent(conflict.Entry),
            MapEvent(conflict.Overlaps),
            conflict.ResourceId,
            conflict.OverlapStart,
            conflict.OverlapEnd,
            conflict.IsOverridden,
            conflict.OverrideNote,
            conflict.CreatedById,
            conflict.CreatedOn,
            conflict.UpdatedById,
            conflict.UpdatedOn,
            conflict.ReportOnEntry,
            conflict.ReportOnOverlaps,
            CreateTitle(conflict)
        );

    private static string CreateTitle(CalendarConflict conflict) =>
        string.Join(
            " ↔ ",
            new[] { conflict.Entry.EventTypeCode, conflict.Overlaps.EventTypeCode }
                .OrderBy(code => code == "leave" ? 0 : 1)
                .ThenBy(code => code, StringComparer.Ordinal)
                .Select(code => code switch
                {
                    "leave" => "Leave",
                    "shift" => "Shift",
                    "assignment" => "Assignment",
                    "training" => "Training",
                    "holiday" => "Holiday",
                    _ => code,
                })
        );

    private static CalendarConflictEventResponse MapEvent(CalendarConflictParticipant participant) =>
        new(
            participant.EventId,
            participant.SourceModule,
            participant.Title,
            participant.Start,
            participant.End,
            participant.SourceEntityId,
            participant.TimeZoneId,
            participant.CanonicalEventId,
            participant.AllDay
        );
}

public sealed record CalendarConflictRejectionResponse(
    string Message,
    IReadOnlyCollection<CalendarConflictResponse> Conflicts
)
{
    public IReadOnlyCollection<CalendarRestrictedConflictWarningResponse> RestrictedWarnings { get; init; } = [];
    public bool HasRestrictedConflicts { get; init; }
}

// Only authorized anchors are public; no identity or metadata from the restricted participant is included.
public sealed record CalendarRestrictedConflictWarningResponse(
    string EventId,
    string SourceModule,
    Guid ResourceId,
    string Message
);
