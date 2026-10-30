import type { CalendarConflict, CalendarConflictEvent } from './calendarTypes';
import { CALENDAR_ALL_DAY_LABEL, formatCalendarDateOnly, formatCalendarDateTimeRange } from '@/utils/date';
import { DateTime } from 'luxon';

export function formatCalendarConflictEventRange(event: CalendarConflictEvent, comparisonTimeZone: string) {
  if (event.allDay) {
    const date = DateTime.fromISO(event.start, { zone: event.timeZoneId || comparisonTimeZone }).toISODate();
    return date ? `${formatCalendarDateOnly(date)}, ${CALENDAR_ALL_DAY_LABEL}` : CALENDAR_ALL_DAY_LABEL;
  }
  return formatCalendarDateTimeRange(event.start, event.end, comparisonTimeZone);
}

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
