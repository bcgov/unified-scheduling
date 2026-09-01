import type { CalendarConflictEvent } from './calendarTypes';
import { formatCalendarDateTimeRange } from '@/utils/date';

export function formatCalendarConflictEventDateTime(event: CalendarConflictEvent, comparisonTimeZone = 'UTC') {
  return formatCalendarDateTimeRange(event.start, event.end, comparisonTimeZone);
}

export function getCalendarConflictEventTimeZoneLabel(event: CalendarConflictEvent, comparisonTimeZone?: string) {
  return event.timeZoneId && event.timeZoneId !== comparisonTimeZone
    ? `Event timezone: ${event.timeZoneId}`
    : undefined;
}
