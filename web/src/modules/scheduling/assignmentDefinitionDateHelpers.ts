import type { AssignmentDefinitionResponse } from '@/api-access/generated/models/assignmentDefinitionResponse';
import { DateTime } from 'luxon';

const fallbackTimeZoneId = 'America/Vancouver';

export function toDefinitionDateInputValue(value?: string | null, timeZoneId?: string | null) {
  if (!value) {
    return '';
  }

  const parsed = DateTime.fromISO(value, { setZone: true }).setZone(resolveDefinitionTimeZoneId(timeZoneId));
  return parsed.isValid ? (parsed.toISODate() ?? '') : '';
}

export function toDefinitionDateTimeOffset(dateInput?: string | null, timeZoneId?: string | null) {
  if (!dateInput) {
    return null;
  }

  const parsed = DateTime.fromISO(dateInput, { zone: resolveDefinitionTimeZoneId(timeZoneId) }).startOf('day');
  return parsed.isValid ? parsed.toUTC().toISO({ suppressMilliseconds: true }) : null;
}

export function isAssignmentDefinitionSelectableForAssignmentDate(
  definition: AssignmentDefinitionResponse,
  assignmentDate: DateTime,
  timeZoneId?: string | null,
) {
  if (!assignmentDate.isValid) {
    return true;
  }

  const targetDate = assignmentDate.setZone(resolveDefinitionTimeZoneId(timeZoneId)).startOf('day');
  const effectiveDate = parseDefinitionDate(definition.effectiveDateUtc, timeZoneId);
  const expiryDate = parseDefinitionDate(definition.expiryDateUtc, timeZoneId);

  return (!effectiveDate || effectiveDate <= targetDate) && (!expiryDate || expiryDate > targetDate);
}

export function assignmentDefinitionOverlapsCalendarDateRange(
  definition: AssignmentDefinitionResponse,
  startDate: string,
  endDate: string,
  timeZoneId?: string | null,
) {
  const zone = resolveDefinitionTimeZoneId(timeZoneId);
  const rangeStart = DateTime.fromISO(startDate, { zone }).startOf('day');
  const rangeEnd = DateTime.fromISO(endDate, { zone }).startOf('day');

  if (!rangeStart.isValid || !rangeEnd.isValid || rangeEnd <= rangeStart) {
    return true;
  }

  const effectiveDate = parseDefinitionDate(definition.effectiveDateUtc, zone);
  const expiryDate = parseDefinitionDate(definition.expiryDateUtc, zone);

  return (!effectiveDate || effectiveDate < rangeEnd) && (!expiryDate || expiryDate > rangeStart);
}

function parseDefinitionDate(value?: string | null, timeZoneId?: string | null) {
  if (!value) {
    return null;
  }

  const parsed = DateTime.fromISO(value, { setZone: true }).setZone(resolveDefinitionTimeZoneId(timeZoneId)).startOf('day');
  return parsed.isValid ? parsed : null;
}

function resolveDefinitionTimeZoneId(timeZoneId?: string | null) {
  return timeZoneId?.trim() || fallbackTimeZoneId;
}
