import type { CalendarConflict, CalendarDataResponse, CalendarEventBase } from './calendarTypes';

export const selectContribution = (response: CalendarDataResponse, contributionId: string) => {
  return response.contributions[contributionId];
};

export const selectCalendarConflicts = (response: CalendarDataResponse): CalendarConflict[] => {
  const data = response.contributions['calendar.events']?.data;
  if (typeof data !== 'object' || data === null || !('conflicts' in data)) {
    return [];
  }

  const conflicts = (data as { conflicts?: unknown }).conflicts;
  return Array.isArray(conflicts) ? (conflicts as CalendarConflict[]) : [];
};

export const resolveCalendarEventId = (event: CalendarEventBase) => {
  const value = (event as CalendarEventBase & { metadata?: { eventId?: unknown } }).metadata?.eventId;
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
};

export const getCalendarConflictsForEvent = (
  eventId: number | null | undefined,
  conflicts: readonly CalendarConflict[],
) => {
  if (eventId == null) {
    return [];
  }

  return conflicts.filter((conflict) => conflict.entry.eventId === eventId || conflict.overlaps.eventId === eventId);
};

export const getCalendarConflictsForEventAndResource = (
  eventId: number | null | undefined,
  resourceId: string,
  conflicts: readonly CalendarConflict[],
) => getCalendarConflictsForEvent(eventId, conflicts).filter((conflict) => conflict.resourceId === resourceId);

export const selectCalendarEvents = (response: CalendarDataResponse): CalendarEventBase[] => {
  return Object.values(response.contributions)
    .flatMap((contribution) => contribution.events)
    .sort((left, right) => {
      const startComparison = left.start.localeCompare(right.start);
      if (startComparison !== 0) {
        return startComparison;
      }

      return left.title.localeCompare(right.title);
    });
};
