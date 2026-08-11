using Unified.Calendar.Models;

namespace Unified.Calendar.Conflicts;

public interface ICalendarConflictService
{
    IReadOnlyCollection<CalendarConflict> DetectConflicts(
        IReadOnlyCollection<CalendarConflictParticipant> participants
    );

    Task<IReadOnlyCollection<CalendarConflict>> GetConflictsAsync(
        CalendarConflictQuery query,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyCollection<CalendarConflict>> CheckCandidatesAsync(
        IReadOnlyCollection<CalendarConflictParticipant> candidates,
        CalendarConflictQuery query,
        CancellationToken cancellationToken = default
    );

    Task<CalendarConflictOverrideResponse> CreateOverrideAsync(
        CalendarConflictOverrideRequest request,
        Guid? createdById,
        CancellationToken cancellationToken = default
    );

    Task InvalidateResolvedOverridesAsync(
        IReadOnlyCollection<int> eventIds,
        Guid? updatedById = null,
        CancellationToken cancellationToken = default
    );
}
