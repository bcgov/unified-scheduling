import { DateTime } from 'luxon';

export const defaultSchedulingTimeZoneId = 'America/Vancouver';

export function resolveSchedulingTimeZoneId(...candidates: Array<string | null | undefined>) {
  return candidates.find(isValidTimeZoneId) ?? defaultSchedulingTimeZoneId;
}

export function resolveSchedulingTimeZoneFromFilters(filters: Record<string, unknown>) {
  const candidate = filters.timeZoneId ?? filters.timeZone;
  return resolveSchedulingTimeZoneId(typeof candidate === 'string' ? candidate : undefined);
}

function isValidTimeZoneId(value: string | null | undefined): value is string {
  return Boolean(value?.trim() && DateTime.local().setZone(value).isValid);
}
