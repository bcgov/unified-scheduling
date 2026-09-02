import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import { isCalendarSchedulingEvent } from './calendarSchedulingData';

export function resolveShiftEntryId(event: CalendarEventBase) {
  if (!isCalendarSchedulingEvent(event)) {
    return null;
  }

  return parsePositiveInteger(event.metadata.shiftEntryId);
}

export function resolveShiftSeriesId(event: CalendarEventBase) {
  if (!isCalendarSchedulingEvent(event)) {
    return null;
  }

  return parsePositiveInteger(event.metadata.shiftSeriesId);
}

export function parsePositiveInteger(value: unknown) {
  let parsed = Number.NaN;
  if (typeof value === 'number') {
    parsed = value;
  } else if (typeof value === 'string') {
    parsed = Number(value);
  }

  return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}
