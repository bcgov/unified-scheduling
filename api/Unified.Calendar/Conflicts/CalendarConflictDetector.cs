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
                .ThenBy(participant => participant.EventId)
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
            .ThenBy(conflict => conflict.Entry.EventId)
            .ThenBy(conflict => conflict.Overlaps.EventId)
            .ThenBy(conflict => conflict.ResourceId)
            .ToList();
    }

    private static bool IsSameEvent(CalendarConflictParticipant left, CalendarConflictParticipant right) =>
        left.EventId == right.EventId;

    private static (CalendarConflictParticipant Entry, CalendarConflictParticipant Overlaps) OrderForDisplay(
        CalendarConflictParticipant left,
        CalendarConflictParticipant right
    )
    {
        var comparison = left.Start.CompareTo(right.Start);
        if (comparison == 0)
            comparison = left.EventId.CompareTo(right.EventId);
        return comparison <= 0 ? (left, right) : (right, left);
    }
}
