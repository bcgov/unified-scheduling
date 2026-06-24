import { DateTime, type DateTimeFormatOptions } from 'luxon';

export const DATE_FORMAT = 'yyyy-MM-dd';

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

export type CalendarDateOptions = {
  /**
   * When true, keeps the zone/offset from the source value.
   * When false, converts to local zone before formatting.
   */
  setZone?: boolean;
};

export type CalendarPeriodValue = 'day' | 'week' | 'work-week' | 'month';

export type CalendarDateRange = {
  startDate: string;
  endDate: string;
};

export function parseDateInput(value: string) {
  return DateTime.fromFormat(value, DATE_FORMAT);
}

export function parseLocalDateTime(value: string) {
  return DateTime.fromISO(value, { zone: 'local' }).startOf('day');
}

export function toDateTime(value: string | Date, timeZone?: string) {
  if (value instanceof Date) {
    const options = timeZone ? { zone: timeZone } : undefined;
    const dateTime = DateTime.fromJSDate(value, options);

    return timeZone ? dateTime.setZone(timeZone) : dateTime;
  }

  const dateTime = DateTime.fromISO(value, { setZone: true });

  return timeZone ? dateTime.setZone(timeZone) : dateTime;
}

export function formatDateTime(value: DateTime, locale: string, format: DateTimeFormatOptions) {
  if (!value.isValid) {
    return '';
  }

  return normalizeMeridiem(value.setLocale(locale).toLocaleString(format));
}

export function normalizeMeridiem(value: string) {
  return value.replace(/\s*a\.m\./i, ' AM').replace(/\s*p\.m\./i, ' PM');
}

export function hasExplicitTimeZoneOffset(value: string) {
  return /(?:z|[+-]\d{2}:?\d{2})$/i.test(value);
}

/**
 * Returns a yyyy-MM-dd calendar date from an ISO-like date value.
 *
 * Zone behavior:
 * - setZone = true (default): keep source offset/zone before extracting date
 * - setZone = false: convert to local zone before extracting date
 *
 * Examples:
 * - toCalendarDateString("2024-01-01T00:30:00Z") => "2024-01-01"
 * - toCalendarDateString("2024-01-01T00:30:00Z", { setZone: false })
 *   may become "2023-12-31" in a negative local timezone
 * - toCalendarDateString("2024-01-01T00:00:00-08:00", { setZone: true }) => "2024-01-01"
 * - "2024-01-01" => "2024-01-01"
 * - null/undefined => null
 */
export function toCalendarDateString(dateValue?: string | null, options?: CalendarDateOptions): string | null {
  if (!dateValue) {
    return null;
  }

  const parsed = DateTime.fromISO(dateValue, { setZone: options?.setZone ?? true });
  return parsed.isValid ? parsed.toISODate() : null;
}

/**
 * Extracts yyyy-MM-dd for form input values.
 *
 * Defaults to preserve source zone/offset to avoid date drift.
 * Pass { setZone: false } to derive date in local zone.
 */
export function toDateInputValue(isoDateString?: string | null, options?: CalendarDateOptions): string | null {
  return toCalendarDateString(isoDateString, options);
}

/**
 * Get today's date in the format yyyy-MM-dd.
 */
export function getTodayDateInputValue(): string {
  return DateTime.now().toFormat(DATE_FORMAT);
}

export function getTodayDateOnly(): string {
  return getTodayDateInputValue();
}

/**
 * Convert date input (yyyy-MM-dd) to API date string.
 * Frontend sends date strings only; backend handles timezone conversion.
 */
export function toApiDateString(dateInput: string): string {
  // Validate the format
  const dt = parseDateInput(dateInput);
  if (!dt.isValid) {
    throw new Error(`Invalid date format. Expected ${DATE_FORMAT}, got ${dateInput}`);
  }
  return dateInput;
}

/**
 * Compare two date inputs (yyyy-MM-dd format).
 * Returns true if left date is before right date.
 */
export function isDateInputBefore(left: string, right: string): boolean {
  const leftDate = parseDateInput(left);
  const rightDate = parseDateInput(right);
  return leftDate.isValid && rightDate.isValid && leftDate < rightDate;
}

/**
 * Check if a date is in the past (yesterday or earlier).
 *
 * Uses the same zone behavior options as toCalendarDateString.
 */
export function isDateInputHistorical(dateInput?: string | null, options?: CalendarDateOptions): boolean {
  const normalizedDateInput = toCalendarDateString(dateInput, options);
  if (!normalizedDateInput) return false;

  const date = parseDateInput(normalizedDateInput);
  const today = DateTime.now().startOf('day');
  return date.isValid && date < today;
}

export function parseLocalDateOnly(value: string): Date {
  return parseLocalDateTime(value).toJSDate();
}

export function formatLocalDateOnly(value: Date): string {
  return DateTime.fromJSDate(value).toISODate() ?? '';
}

export function addDays(value: string, days: number): string {
  return parseLocalDateTime(value).plus({ days }).toISODate() ?? value;
}

export function addMonths(value: string, months: number): string {
  return parseLocalDateTime(value).plus({ months }).toISODate() ?? value;
}

export function startOfWeek(value: string): string {
  return parseLocalDateTime(value).startOf('week').toISODate() ?? value;
}

export function startOfMonth(value: string): string {
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

export function shiftDateRange(
  currentStartDate: string,
  currentPeriod: CalendarPeriodValue,
  direction: -1 | 1,
): CalendarDateRange {
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

export function formatRangeLabel(startDate: string, endDate: string, currentPeriod: CalendarPeriodValue): string {
  const start = parseLocalDateTime(startDate);

  if (currentPeriod === 'month') {
    return formatDateTime(start, 'en-CA', calendarMonthFormat);
  }

  if (currentPeriod === 'day') {
    return formatDateTime(start, 'en-CA', calendarDateOnlyFormat);
  }

  return `${formatDateTime(start, 'en-CA', calendarDateOnlyFormat)} - ${formatDateTime(parseLocalDateTime(addDays(endDate, -1)), 'en-CA', calendarDateOnlyFormat)}`;
}

export function localDateOnlyToUtcInstant(value: string): string {
  return DateTime.fromISO(value, { zone: 'utc' }).startOf('day').toISO() ?? '';
}

export function toCalendarDateOnly(value?: string | null): string | undefined {
  return toCalendarDateString(value) ?? undefined;
}

export function formatCalendarDateOnly(value: string, locale = 'en-CA'): string {
  return formatDateTime(parseLocalDateTime(value), locale, calendarDateOnlyFormat);
}

export function formatCalendarDateTimeDate(value: string | Date, timeZone = 'UTC', locale = 'en-CA'): string {
  return formatDateTime(toDateTime(value, timeZone), locale, calendarDateOnlyFormat);
}

export function formatCalendarTime(value: string | Date, timeZone = 'UTC', locale = 'en-CA'): string {
  return formatDateTime(toDateTime(value, timeZone), locale, calendarTimeFormat);
}

export function formatCalendarEventDate(
  value: string,
  options: {
    allDay?: boolean;
    timeZone?: string;
    locale?: string;
  } = {},
): string {
  const { allDay = false, timeZone = 'UTC', locale = 'en-CA' } = options;

  return allDay ? formatCalendarDateOnly(value, locale) : formatCalendarDateTimeDate(value, timeZone, locale);
}

export function formatCalendarEventTimeRange(
  start: string | Date,
  end?: string | Date,
  options: {
    allDay?: boolean;
    timeZone?: string;
    locale?: string;
    allDayLabel?: string;
  } = {},
): string {
  const { allDay = false, timeZone, locale = 'en-CA', allDayLabel = 'All day' } = options;

  if (allDay) {
    return allDayLabel;
  }

  const formattedStart = formatCalendarTime(start, timeZone, locale);
  const formattedEnd = end ? formatCalendarTime(end, timeZone, locale) : undefined;

  return formattedEnd ? `${formattedStart} - ${formattedEnd}` : formattedStart;
}
