import type { CalendarConflict } from '@/modules/calendar/calendarTypes';

export function getConflictsForEvent(eventId: number | null | undefined, conflicts: readonly CalendarConflict[]) {
  if (eventId == null) {
    return [];
  }

  return conflicts.filter(
    (conflict) => conflict.entry.eventId === eventId || conflict.overlaps.eventId === eventId,
  );
}

export function getConflictsForEventAndResource(
  eventId: number | null | undefined,
  resourceId: string,
  conflicts: readonly CalendarConflict[],
) {
  return getConflictsForEvent(eventId, conflicts).filter((conflict) => conflict.resourceId === resourceId);
}
