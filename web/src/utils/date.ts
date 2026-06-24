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
 * Extracts HH:mm time from an ISO datetime string, preserving source zone/offset.
 * Returns null for full-day (midnight, 00:00) times or if the input is invalid.
 * Pass { setZone: false } to derive time in local zone.
 */
export function toTimeInputValue(isoDateString?: string | null, options?: CalendarDateOptions): string | null {
  if (!isoDateString) return null;

  const parsed = DateTime.fromISO(isoDateString, { setZone: options?.setZone ?? true });
  if (!parsed.isValid) return null;

  const hh = String(parsed.hour).padStart(2, '0');
  const mm = String(parsed.minute).padStart(2, '0');
  const time = `${hh}:${mm}`;

  // Treat midnight (00:00) as full-day — no time component
  return time === '00:00' ? null : time;
}

/**
 * Determines if a datetime range represents a full-day entry.
 *
 * Mirrors the isDateFullday filter from sheriff-scheduling:
 * - Both times at midnight (00:00) → full day
 * - OR the range spans ≥ 1439 minutes (~24 h) in either direction → full day
 *
 * Returns true when either value is absent (treat unknown as full-day).
 */
export function isDateTimeFullDay(startIso?: string | null, endIso?: string | null): boolean {
  if (toTimeInputValue(startIso) === null && toTimeInputValue(endIso) === null) {
    return true;
  }

  if (startIso && endIso) {
    const start = DateTime.fromISO(startIso, { setZone: true });
    const end = DateTime.fromISO(endIso, { setZone: true });
    if (start.isValid && end.isValid) {
      const durationMinutes = Math.abs(end.diff(start, 'minutes').minutes);
      if (durationMinutes >= 1439) {
        return true;
      }
    }
  }

  return false;
}

/**
 * Combines a date input (yyyy-MM-dd) and an optional time input (HH:mm) into a
 * local datetime string (yyyy-MM-ddTHH:mm) suitable for the API.
 *
 * When no time is supplied, midnight (T00:00) is used — representing a full-day entry.
 *
 * Examples:
 * - ('2026-01-10', '08:30') => '2026-01-10T08:30'
 * - ('2026-01-10', '')      => '2026-01-10T00:00'
 * - ('2026-01-10')          => '2026-01-10T00:00'
 */
export function toLocalDateTimeString(date: string, time?: string): string {
  const d = DateTime.fromFormat(date, DATE_FORMAT);
  if (!d.isValid) {
    throw new Error(`Invalid date: ${date}`);
  }

  if (time) {
    const t = DateTime.fromFormat(time, 'HH:mm');
    if (!t.isValid) {
      throw new Error(`Invalid time: ${time}`);
    }
    return d.set({ hour: t.hour, minute: t.minute }).toFormat(`${DATE_FORMAT}'T'HH:mm`);
  }

  return d.toFormat(`${DATE_FORMAT}'T'HH:mm`);
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
