import { DateTime, type DateTimeFormatOptions } from 'luxon';

type CalendarPeriodValue = 'day' | 'week' | 'work-week' | 'month';

export type CalendarDateRange = {
  startDate: string;
  endDate: string;
};

const calendarDateOnlyFormat = {
  month: 'long',
  day: 'numeric',
  year: 'numeric',
} as const satisfies DateTimeFormatOptions;

const calendarMonthFormat = {
  month: 'long',
  year: 'numeric',
} as const satisfies DateTimeFormatOptions;

const calendarTimeFormat = {
  hour: 'numeric',
  minute: '2-digit',
} as const satisfies DateTimeFormatOptions;

function parseLocalDateTime(value: string) {
  return DateTime.fromISO(value, { zone: 'local' }).startOf('day');
}

function toDateTime(value: string | Date, timeZone?: string) {
  const dateTime =
    value instanceof Date
      ? DateTime.fromJSDate(value, timeZone ? { zone: timeZone } : undefined)
      : DateTime.fromISO(value, { setZone: true });

  return timeZone ? dateTime.setZone(timeZone) : dateTime;
}

function formatDateTime(value: DateTime, locale: string, format: DateTimeFormatOptions) {
  return value.setLocale(locale).toLocaleString(format);
}

export function parseLocalDateOnly(value: string) {
  return parseLocalDateTime(value).toJSDate();
}

export function formatLocalDateOnly(value: Date) {
  return DateTime.fromJSDate(value).toISODate() ?? '';
}

export function getTodayDateOnly() {
  return DateTime.local().toISODate() ?? '';
}

export function addDays(value: string, days: number) {
  return parseLocalDateTime(value).plus({ days }).toISODate() ?? value;
}

export function addMonths(value: string, months: number) {
  return parseLocalDateTime(value).plus({ months }).toISODate() ?? value;
}

export function startOfWeek(value: string) {
  return parseLocalDateTime(value).startOf('week').toISODate() ?? value;
}

export function startOfMonth(value: string) {
  return parseLocalDateTime(value).startOf('month').toISODate() ?? value;
}

export function buildDateRangeForPeriod(anchorDate: string, currentPeriod: CalendarPeriodValue): CalendarDateRange {
  switch (currentPeriod) {
    case 'day':
      return {
        startDate: anchorDate,
        endDate: addDays(anchorDate, 1),
      };
    case 'work-week': {
      const startDate = startOfWeek(anchorDate);
      return {
        startDate,
        endDate: addDays(startDate, 5),
      };
    }
    case 'month': {
      const startDate = startOfMonth(anchorDate);
      return {
        startDate,
        endDate: addMonths(startDate, 1),
      };
    }
    case 'week':
    default: {
      const startDate = startOfWeek(anchorDate);
      return {
        startDate,
        endDate: addDays(startDate, 7),
      };
    }
  }
}

export function shiftDateRange(currentStartDate: string, currentPeriod: CalendarPeriodValue, direction: -1 | 1) {
  switch (currentPeriod) {
    case 'day':
      return buildDateRangeForPeriod(addDays(currentStartDate, direction), currentPeriod);
    case 'work-week':
    case 'week':
      return buildDateRangeForPeriod(addDays(currentStartDate, direction * 7), currentPeriod);
    case 'month':
      return buildDateRangeForPeriod(addMonths(currentStartDate, direction), currentPeriod);
  }
}

export function getInitialCalendarDateRange(): CalendarDateRange {
  return buildDateRangeForPeriod(getTodayDateOnly(), 'week');
}

export function formatRangeLabel(startDate: string, endDate: string, currentPeriod: CalendarPeriodValue) {
  const start = parseLocalDateTime(startDate);

  if (currentPeriod === 'month') {
    return formatDateTime(start, 'en-CA', calendarMonthFormat);
  }

  if (currentPeriod === 'day') {
    return formatDateTime(start, 'en-CA', calendarDateOnlyFormat);
  }

  return `${formatDateTime(start, 'en-CA', calendarDateOnlyFormat)} - ${formatDateTime(parseLocalDateTime(addDays(endDate, -1)), 'en-CA', calendarDateOnlyFormat)}`;
}

export function localDateOnlyToUtcInstant(value: string) {
  return DateTime.fromISO(value, { zone: 'utc' }).startOf('day').toISO() ?? '';
}

export function toCalendarDateOnly(value?: string | null) {
  if (!value) {
    return undefined;
  }

  return DateTime.fromISO(value, { setZone: true }).toISODate() ?? undefined;
}

export function formatCalendarDateOnly(value: string, locale = 'en-CA') {
  return formatDateTime(parseLocalDateTime(value), locale, calendarDateOnlyFormat);
}

export function formatCalendarDateTimeDate(value: string | Date, timeZone = 'UTC', locale = 'en-CA') {
  return formatDateTime(toDateTime(value, timeZone), locale, calendarDateOnlyFormat);
}

export function formatCalendarTime(value: string | Date, timeZone = 'UTC', locale = 'en-CA') {
  return formatDateTime(toDateTime(value, timeZone), locale, calendarTimeFormat);
}
