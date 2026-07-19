import type { AssignmentSeriesResponse } from '@/api-access/generated/models/assignmentSeriesResponse';
import type { ShiftSeriesLinkFormData } from './calendarSchedulingAssignmentForm';
import { filterAssignedUserIds } from './calendarSchedulingLinkMappers';

export function resolveShiftSeriesLinksFromAssignmentSeries(
  series: AssignmentSeriesResponse,
): ShiftSeriesLinkFormData[] {
  return (series.shiftSeriesLinks ?? []).flatMap((link) => {
    const shiftSeriesId = Number(link.shiftSeriesId);
    if (!Number.isInteger(shiftSeriesId) || shiftSeriesId <= 0) {
      return [];
    }

    return [
      {
        shiftSeriesId,
        assignedUserIds: filterAssignedUserIds(link),
      },
    ];
  });
}
