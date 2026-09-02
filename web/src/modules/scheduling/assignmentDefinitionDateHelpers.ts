import type { AssignmentDefinitionResponse } from '@/api-access/generated/models/assignmentDefinitionResponse';
import { DateTime } from 'luxon';
import { resolveSchedulingTimeZoneId } from './schedulingTimeZone';

export function toUtcBusinessDateInput(value?: string | null) {
  if (!value) {
    return '';
  }

  const parsed = DateTime.fromISO(value, { setZone: true });
  return parsed.isValid ? (parsed.toUTC().toISODate() ?? '') : '';
}

export function fromUtcBusinessDateInput(dateInput?: string | null) {
  if (!dateInput) {
    return null;
  }

  const parsed = DateTime.fromISO(dateInput, { zone: 'utc' }).startOf('day');
  return parsed.isValid ? parsed.toISO({ suppressMilliseconds: true }) : null;
}

export function isAssignmentDefinitionSelectableForAssignmentDate(
  definition: AssignmentDefinitionResponse,
  assignmentDate: DateTime,
  timeZoneId?: string | null,
) {
  if (!assignmentDate.isValid) {
    return true;
  }

  const targetDate = assignmentDate.setZone(resolveSchedulingTimeZoneId(timeZoneId)).toISODate();
  if (!targetDate) {
    return true;
  }

  const effectiveDate = parseDefinitionDate(definition.effectiveDateUtc);
  const expiryDate = parseDefinitionDate(definition.expiryDateUtc);

  return (!effectiveDate || effectiveDate <= targetDate) && (!expiryDate || expiryDate > targetDate);
}

export function assignmentDefinitionOverlapsCalendarDateRange(
  definition: AssignmentDefinitionResponse,
  startDate: string,
  endDate: string,
  timeZoneId?: string | null,
) {
  const zone = resolveSchedulingTimeZoneId(timeZoneId);
  const rangeStart = DateTime.fromISO(startDate, { zone }).toISODate();
  const rangeEnd = DateTime.fromISO(endDate, { zone }).toISODate();

  if (!rangeStart || !rangeEnd || rangeEnd <= rangeStart) {
    return true;
  }

  const effectiveDate = parseDefinitionDate(definition.effectiveDateUtc);
  const expiryDate = parseDefinitionDate(definition.expiryDateUtc);

  return (!effectiveDate || effectiveDate < rangeEnd) && (!expiryDate || expiryDate > rangeStart);
}

function parseDefinitionDate(value?: string | null) {
  if (!value) {
    return null;
  }

  return toUtcBusinessDateInput(value) || null;
}
