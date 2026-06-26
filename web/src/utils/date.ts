import { DateTime } from 'luxon';

const DATE_FORMAT = 'yyyy-MM-dd';

export type CalendarDateOptions = {
  /**
   * When true, keeps the zone/offset from the source value.
   * When false, converts to local zone before formatting.
   */
  setZone?: boolean;
};

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

/**
 * Convert date input (yyyy-MM-dd) to API date string.
 * Frontend sends date strings only; backend handles timezone conversion.
 */
export function toApiDateString(dateInput: string): string {
  // Validate the format
  const dt = DateTime.fromFormat(dateInput, DATE_FORMAT);
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
  const leftDate = DateTime.fromFormat(left, DATE_FORMAT);
  const rightDate = DateTime.fromFormat(right, DATE_FORMAT);
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

  const date = DateTime.fromFormat(normalizedDateInput, DATE_FORMAT);
  const today = DateTime.now().startOf('day');
  return date.isValid && date < today;
}
