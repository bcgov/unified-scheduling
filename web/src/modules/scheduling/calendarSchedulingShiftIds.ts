import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import { isCalendarSchedulingEvent } from './calendarSchedulingData';

export function resolveShiftEntryId(event: CalendarEventBase) {
  if (!isCalendarSchedulingEvent(event)) {
    return null;
  }

  return parseNumericId(event.metadata.shiftEntryId);
}

export function resolveShiftSeriesId(event: CalendarEventBase) {
  if (!isCalendarSchedulingEvent(event)) {
    return null;
  }

  return parseNumericId(event.metadata.shiftSeriesId);
}

export function parseNumericId(value: string | number | null | undefined) {
  if (value == null) {
    return null;
  }

  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}
