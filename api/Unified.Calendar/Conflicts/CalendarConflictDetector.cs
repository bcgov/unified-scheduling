using Unified.Common.Calendar.Conflicts;

namespace Unified.Calendar.Conflicts;

public static class CalendarConflictDetector
{
    public static IReadOnlyCollection<CalendarConflict> Detect(
        IReadOnlyCollection<CalendarConflictParticipant> participants
    )
    {
        var conflicts = new List<CalendarConflict>();

        foreach (
            var resourceGroup in participants.GroupBy(participant => participant.ResourceId).OrderBy(group => group.Key)
        )
        {
            var ordered = resourceGroup
                .Where(participant => participant.End > participant.Start)
                .OrderBy(participant => participant.Start)
                .ThenBy(participant => participant.End)
                .ThenBy(participant => participant.SourceModule, StringComparer.Ordinal)
                .ThenBy(participant => participant.EventId, StringComparer.Ordinal)
                .ToList();

            for (var leftIndex = 0; leftIndex < ordered.Count; leftIndex++)
            {
                var left = ordered[leftIndex];
                for (var rightIndex = leftIndex + 1; rightIndex < ordered.Count; rightIndex++)
                {
                    var right = ordered[rightIndex];
                    if (right.Start >= left.End)
                        break;

                    if (IsSameEvent(left, right))
                        continue;

                    if (!IsEligiblePair(left, right))
                        continue;

                    if (!HaveCompatibleTimeZones(left, right))
                        continue;

                    var (entry, overlaps) = OrderForDisplay(left, right);
                    conflicts.Add(
                        new CalendarConflict(
                            entry,
                            overlaps,
                            resourceGroup.Key,
                            left.Start > right.Start ? left.Start : right.Start,
                            left.End < right.End ? left.End : right.End
                        )
                    );
                }
            }
        }

        return conflicts
            .OrderBy(conflict => conflict.OverlapStart)
            .ThenBy(conflict => conflict.Entry.SourceModule, StringComparer.Ordinal)
            .ThenBy(conflict => conflict.Entry.EventId, StringComparer.Ordinal)
            .ThenBy(conflict => conflict.Overlaps.SourceModule, StringComparer.Ordinal)
            .ThenBy(conflict => conflict.Overlaps.EventId, StringComparer.Ordinal)
            .ThenBy(conflict => conflict.ResourceId)
            .ToList();
    }

    private static bool IsSameEvent(CalendarConflictParticipant left, CalendarConflictParticipant right) =>
        left.Identity == right.Identity
        || (
            left.SourceModule == right.SourceModule
            && string.Equals(
                left.CanonicalEventId ?? left.EventId,
                right.CanonicalEventId ?? right.EventId,
                StringComparison.Ordinal
            )
        );

    // Leave initially blocks scheduling only; do not introduce leave/leave or leave/holiday conflicts.
    private static bool IsEligiblePair(CalendarConflictParticipant left, CalendarConflictParticipant right) =>
        (left.ConflictsWithSourceModule is null || left.ConflictsWithSourceModule == right.SourceModule)
        && (right.ConflictsWithSourceModule is null || right.ConflictsWithSourceModule == left.SourceModule)
        && (
            left.SourceModule == "leave"
                ? right.SourceModule == "scheduling"
                : right.SourceModule != "leave" || left.SourceModule == "scheduling"
        );

    private static bool HaveCompatibleTimeZones(CalendarConflictParticipant left, CalendarConflictParticipant right) =>
        (!left.RequiresMatchingTimeZone && !right.RequiresMatchingTimeZone) || HasSameTimeZone(left, right);

    private static bool HasSameTimeZone(CalendarConflictParticipant left, CalendarConflictParticipant right) =>
        !string.IsNullOrWhiteSpace(left.TimeZoneId)
        && !string.IsNullOrWhiteSpace(right.TimeZoneId)
        && string.Equals(left.TimeZoneId, right.TimeZoneId, StringComparison.OrdinalIgnoreCase);

    private static (CalendarConflictParticipant Entry, CalendarConflictParticipant Overlaps) OrderForDisplay(
        CalendarConflictParticipant left,
        CalendarConflictParticipant right
    )
    {
        var comparison = left.Start.CompareTo(right.Start);
        if (comparison == 0)
            comparison = StringComparer.Ordinal.Compare(left.SourceModule, right.SourceModule);
        if (comparison == 0)
            comparison = StringComparer.Ordinal.Compare(left.EventId, right.EventId);
        return comparison <= 0 ? (left, right) : (right, left);
    }
}
