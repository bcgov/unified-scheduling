import type { ShiftResourceFormData } from './calendarSchedulingShiftForm';
import { syncAssignmentEntryLinks, syncAssignmentSeriesLinks } from './calendarSchedulingShiftAssignmentApi';

export async function syncCreatedShiftAssignmentLinks(
  kind: 'entry' | 'series',
  shiftId: number,
  formData: ShiftResourceFormData,
) {
  if (kind === 'entry') {
    for (const link of formData.assignmentEntryLinks ?? []) {
      if (link.assignmentEntryId) {
        await syncAssignmentEntryLinks(
          link.assignmentEntryId,
          [{ shiftEntryId: shiftId, assignedUserIds: link.assignedUserIds ?? [] }],
          [],
        );
      }
    }
    return;
  }

  for (const link of formData.assignmentSeriesLinks ?? []) {
    if (link.assignmentSeriesId) {
      await syncAssignmentSeriesLinks(
        link.assignmentSeriesId,
        [{ shiftSeriesId: shiftId, assignedUserIds: link.assignedUserIds ?? [] }],
        [],
      );
    }
  }
}
