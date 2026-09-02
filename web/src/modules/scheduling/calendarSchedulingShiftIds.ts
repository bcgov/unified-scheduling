import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import { isCalendarSchedulingEvent } from './calendarSchedulingData';

export function resolveShiftEntryId(event: CalendarEventBase) {
  if (!isCalendarSchedulingEvent(event)) {
    return null;
  }

  return parsePositiveInteger(event.metadata.shiftEntryId);
}

export function resolveShiftSeriesId(event: CalendarEventBase) {
  if (!isCalendarSchedulingEvent(event)) {
    return null;
  }

  return parsePositiveInteger(event.metadata.shiftSeriesId);
}

export function resolveAssignmentEntryId(event: CalendarEventBase) {
  if (!isCalendarSchedulingEvent(event)) {
    return undefined;
  }

  return parsePositiveInteger(event.metadata.assignmentEntryId) ?? undefined;
}

export function resolveAssignmentSeriesId(event: CalendarEventBase) {
  if (!isCalendarSchedulingEvent(event)) {
    return undefined;
  }

  return parsePositiveInteger(event.metadata.assignmentSeriesId) ?? undefined;
}

export function isShiftEvent(event: CalendarEventBase) {
  return event.type === 'scheduling.shift' || event.eventTypeCode === 'shift' || resolveShiftEntryId(event) !== null;
}

export function isAssignmentEvent(event: CalendarEventBase) {
  return (
    event.type === 'scheduling.assignment' ||
    event.eventTypeCode === 'assignment' ||
    resolveAssignmentEntryId(event) !== undefined
  );
}

export function createAssignmentResourceId(assignmentDefinitionId: number) {
  return `assignment-definition-${assignmentDefinitionId}`;
}

export function parsePositiveInteger(value: unknown) {
  let parsed = Number.NaN;
  if (typeof value === 'number') {
    parsed = value;
  } else if (typeof value === 'string') {
    parsed = Number(value);
  }

  return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}
