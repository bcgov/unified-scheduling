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
                .ThenBy(participant => participant.EventId ?? int.MaxValue)
                .ThenBy(participant => participant.EventTypeCode, StringComparer.Ordinal)
                .ThenBy(participant => participant.Title, StringComparer.Ordinal)
                .ToList();

            for (var leftIndex = 0; leftIndex < ordered.Count; leftIndex++)
            {
                var left = ordered[leftIndex];
                for (var rightIndex = leftIndex + 1; rightIndex < ordered.Count; rightIndex++)
                {
                    var right = ordered[rightIndex];
                    if (right.Start >= left.End)
                        break;

                    if (IsSameEvent(left, right) || left.Start >= right.End)
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
            .ThenBy(conflict => conflict.Entry.EventId ?? int.MaxValue)
            .ThenBy(conflict => conflict.Overlaps.EventId ?? int.MaxValue)
            .ThenBy(conflict => conflict.ResourceId)
            .ToList();
    }

    private static bool IsSameEvent(CalendarConflictParticipant left, CalendarConflictParticipant right) =>
        ReferenceEquals(left, right) || left == right || (left.EventId.HasValue && left.EventId == right.EventId);

    private static (CalendarConflictParticipant Entry, CalendarConflictParticipant Overlaps) OrderForDisplay(
        CalendarConflictParticipant left,
        CalendarConflictParticipant right
    )
    {
        var comparison = left.Start.CompareTo(right.Start);
        if (comparison == 0 && left.EventId.HasValue && right.EventId.HasValue)
            comparison = left.EventId.Value.CompareTo(right.EventId.Value);
        if (comparison == 0)
            comparison = string.Compare(left.Title, right.Title, StringComparison.Ordinal);

        return comparison <= 0 ? (left, right) : (right, left);
    }
}
