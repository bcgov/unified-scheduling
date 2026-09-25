import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import { isCalendarSchedulingEvent } from './calendarSchedulingData';

export function resolveCalendarEventUserIds(
  event: CalendarEventBase,
  options: { fallbackToResourceIds?: boolean } = {},
) {
  if (!isCalendarSchedulingEvent(event)) {
    return event.resourceIds ?? [];
  }

  if (event.metadata.userIds?.length) {
    return event.metadata.userIds;
  }

  if (event.metadata.userId) {
    return [event.metadata.userId];
  }

  return options.fallbackToResourceIds === false ? [] : (event.resourceIds ?? []);
}
