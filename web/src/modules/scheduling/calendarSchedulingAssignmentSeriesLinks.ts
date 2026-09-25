import type { AssignmentSeriesResponse } from '@/api-access/generated/models/assignmentSeriesResponse';
import type { ShiftSeriesLinkFormData } from './calendarSchedulingAssignmentForm';
import { mapLoadedAssignedUserLinks } from './calendarSchedulingLinkMappers';

export function resolveShiftSeriesLinksFromAssignmentSeries(
  series: AssignmentSeriesResponse,
): ShiftSeriesLinkFormData[] {
  return mapLoadedAssignedUserLinks(series.shiftSeriesLinks ?? [], 'shiftSeriesId', (link) => link.shiftSeriesId);
}
