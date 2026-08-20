import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';

export interface CalendarEventMetadata {
  shiftEntryId?: string;
  shiftSeriesId?: number;
  eventId?: number;
  userId?: string;
  userIds?: string[];
}

export interface CalendarSchedulingEvent extends CalendarEventBase {
  isConflict?: boolean;
  metadata: CalendarEventMetadata;
}

export function isCalendarSchedulingEvent(event: CalendarEventBase): event is CalendarSchedulingEvent {
  return 'metadata' in event && typeof event.metadata === 'object' && event.metadata !== null;
}
