import type { AssignmentDefinitionResponse } from '@/api-access/generated/models/assignmentDefinitionResponse';
import { addDays } from '@/utils/date';
import { fromUtcBusinessDate, toUtcBusinessDate } from '@/utils/utcBusinessDate';
import { DateTime } from 'luxon';
import { resolveSchedulingTimeZoneId } from './schedulingTimeZone';

export function toUtcBusinessDateInput(value?: string | null) {
  return toUtcBusinessDate(value) ?? '';
}

export function fromUtcBusinessDateInput(dateInput?: string | null) {
  return fromUtcBusinessDate(dateInput);
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

  return assignmentDefinitionOverlapsCalendarDateRange(definition, targetDate, addDays(targetDate, 1), timeZoneId);
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
  return toUtcBusinessDate(value);
}
