import { DateTime } from 'luxon';

/**
 * UTC business dates are date-only values stored as UTC midnight (for example effective/expiry dates).
 * They are never shifted by a location or browser time zone; compare them to a local calendar day
 * (`yyyy-MM-dd`) resolved in the relevant time zone.
 */

/** Returns the `yyyy-MM-dd` UTC day of an ISO value, or null when missing or invalid. */
export function toUtcBusinessDate(value?: string | null): string | null {
  if (!value) return null;

  const parsed = DateTime.fromISO(value, { setZone: true });
  return parsed.isValid ? parsed.toUTC().toISODate() : null;
}

/** Returns UTC midnight ISO for a `yyyy-MM-dd` day, or null when missing or invalid. */
export function fromUtcBusinessDate(dateInput?: string | null): string | null {
  if (!dateInput) return null;

  const parsed = DateTime.fromISO(dateInput, { zone: 'utc' }).startOf('day');
  return parsed.isValid ? parsed.toISO({ suppressMilliseconds: true }) : null;
}

/** Effective is inclusive, expiry is exclusive; a missing bound is open. */
export function isUtcBusinessDateRangeActiveOn(
  effectiveDateUtc: string | null | undefined,
  expiryDateUtc: string | null | undefined,
  localDate: string,
): boolean {
  const effectiveDate = toUtcBusinessDate(effectiveDateUtc);
  const expiryDate = toUtcBusinessDate(expiryDateUtc);

  return (!effectiveDate || effectiveDate <= localDate) && (!expiryDate || expiryDate > localDate);
}
