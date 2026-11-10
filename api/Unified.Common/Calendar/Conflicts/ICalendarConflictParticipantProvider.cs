namespace Unified.Common.Calendar.Conflicts;

public interface ICalendarConflictParticipantProvider
{
    Task<CalendarConflictParticipant?> GetParticipantAsync(
        CalendarConflictEventIdentity identity,
        Guid resourceId,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyCollection<CalendarConflictParticipant>> GetParticipantsAsync(
        CalendarConflictQuery query,
        CancellationToken cancellationToken = default
    );

    // Providers own event-type and linkage knowledge. Unknown types are restricted by default.
    Task<CalendarConflictParticipantVisibility?> GetVisibilityAsync(
        CalendarConflictParticipant participant,
        IReadOnlySet<string> viewerPermissions,
        CancellationToken cancellationToken = default
    ) => Task.FromResult<CalendarConflictParticipantVisibility?>(null);
}

public sealed record CalendarConflictParticipantVisibility(
    bool CanView,
    IReadOnlyCollection<CalendarConflictEventIdentity> VisibleAnchors
);

/// <summary>
/// Identifies providers whose participants depend on resource contexts supplied by other providers or mutation
/// candidates. Contextual providers are loaded after ordinary providers so global query values are not treated as
/// resource-specific applicability.
/// </summary>
public interface IContextualCalendarConflictParticipantProvider : ICalendarConflictParticipantProvider;
