import type { ApiCalendarEventResponse } from '@/api-access/calendar';
import { toCalendarDateOnly } from '../calendarDateUtils';
import type { CalendarEventBase } from '../calendarTypes';

export function mapApiCalendarEventToCalendarEventBase(apiEvent: ApiCalendarEventResponse): CalendarEventBase {
  const start = apiEvent.allDay
    ? (toCalendarDateOnly(apiEvent.startAtUtc) ?? apiEvent.startAtUtc)
    : apiEvent.startAtUtc;
  const end = apiEvent.allDay ? toCalendarDateOnly(apiEvent.endAtUtc) : apiEvent.endAtUtc;
  const eventTypeCode = apiEvent.eventTypeCode || 'general';

  return {
    id: String(apiEvent.id),
    type: `calendar.${eventTypeCode}`,
    sourceModule: apiEvent.sourceModule,
    title: apiEvent.title,
    description: apiEvent.description,
    notes: apiEvent.notes,
    color: apiEvent.color,
    start,
    end,
    seriesStartAtUtc: apiEvent.seriesStartAtUtc,
    seriesEndAtUtc: apiEvent.seriesEndAtUtc,
    allDay: apiEvent.allDay,
    isException: apiEvent.isException,
    eventTypeCode,
    statusTypeCode: apiEvent.statusTypeCode,
    cancelledAt: apiEvent.cancelledAt,
    cancelledByUserId: apiEvent.cancelledByUserId,
    cancellationReason: apiEvent.cancellationReason,
    timeZoneId: apiEvent.timeZoneId,
    status: apiEvent.status,
    locationId: apiEvent.locationId,
  };
}
