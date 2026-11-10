import type { CalendarEventBase, CalendarQueryContext } from '@/modules/calendar/calendarTypes';
import { DATE_FORMAT, hasExplicitTimeZoneOffset, toCalendarDateOnly, toDateTime } from '@/utils/date';
import { resolveSchedulingTimeZoneFromFilters } from './schedulingTimeZone';

export function getCalendarEventDateKey(
  value: string | Date | undefined | null,
  timeZone?: string,
  locale = 'en-CA',
): string | undefined {
  if (!value) {
    return undefined;
  }

  if (typeof value === 'string') {
    const dateOnly = toCalendarDateOnly(value);

    if (!dateOnly || !/^\d{4}-\d{2}-\d{2}$/.test(dateOnly)) {
      return undefined;
    }

    if (!timeZone || !hasExplicitTimeZoneOffset(value)) {
      return dateOnly;
    }
  }

  const dateTime = toDateTime(value, timeZone);

  if (!dateTime.isValid) {
    return undefined;
  }

  return dateTime.setLocale(locale).toFormat(DATE_FORMAT);
}

export function eventStartsInCalendarContext(
  event: CalendarEventBase,
  context: Pick<CalendarQueryContext, 'startDate' | 'endDate' | 'filters'>,
) {
  const date = getCalendarEventDateKey(
    event.start,
    event.timeZoneId ?? resolveSchedulingTimeZoneFromFilters(context.filters),
  );
  return date !== undefined && date >= context.startDate && date < context.endDate;
}
