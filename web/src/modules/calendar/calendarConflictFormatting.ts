import type { CalendarConflict, CalendarConflictEvent } from './calendarTypes';
import { formatCalendarDateTimeRange } from '@/utils/date';

export function formatCalendarConflictEventDateTime(event: CalendarConflictEvent, comparisonTimeZone: string) {
  return formatCalendarDateTimeRange(event.start, event.end, comparisonTimeZone);
}

export function getCalendarConflictEventTimeZoneLabel(event: CalendarConflictEvent, comparisonTimeZone: string) {
  return event.timeZoneId && event.timeZoneId !== comparisonTimeZone
    ? `Event timezone: ${event.timeZoneId}`
    : undefined;
}

export function resolveCalendarConflictSides(conflict: CalendarConflict, currentEventId: number) {
  const currentEventIsOverlap = conflict.overlaps.eventId === currentEventId;
  return {
    current: currentEventIsOverlap ? conflict.overlaps : conflict.entry,
    other: currentEventIsOverlap ? conflict.entry : conflict.overlaps,
  };
}
