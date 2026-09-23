using Unified.Calendar.Models;

namespace Unified.Calendar.Conflicts;

public interface ICalendarConflictService
{
    Task<IReadOnlyCollection<CalendarConflict>> GetConflictsAsync(
        CalendarConflictQuery query,
        CancellationToken cancellationToken = default
    );

    Task EnsureNoUnresolvedConflictsAsync(
        IReadOnlyCollection<CalendarConflictParticipant> candidates,
        CancellationToken cancellationToken = default
    );

    Task ValidateAndApplyConflictAcknowledgementsAsync(
        IReadOnlyCollection<CalendarConflictParticipant> candidates,
        IReadOnlyCollection<CalendarConflictAcknowledgement>? acknowledgements,
        Guid? actorId,
        CancellationToken cancellationToken = default
    );

    Task CreateOverrideAsync(
        CalendarConflictAcknowledgement acknowledgement,
        Guid? createdById,
        CancellationToken cancellationToken = default
    );

    Task InvalidateResolvedOverridesAsync(
        IReadOnlyCollection<int> eventIds,
        Guid? updatedById = null,
        CancellationToken cancellationToken = default
    );
}
