import type { CalendarConflict, CalendarConflictEvent } from './calendarTypes';

export function getCalendarConflictEventTimeZoneLabel(event: CalendarConflictEvent, comparisonTimeZone: string) {
  return event.timeZoneId && event.timeZoneId !== comparisonTimeZone
    ? `Event timezone: ${event.timeZoneId}`
    : undefined;
}

export function resolveCalendarConflictSides(conflict: CalendarConflict, currentEventId: number) {
  const currentEventIsOverlap = conflict.overlaps.eventId === String(currentEventId);
  return {
    current: currentEventIsOverlap ? conflict.overlaps : conflict.entry,
    other: currentEventIsOverlap ? conflict.entry : conflict.overlaps,
  };
}
