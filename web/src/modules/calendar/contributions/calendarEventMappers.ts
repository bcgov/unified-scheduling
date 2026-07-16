import { calendarEventTypes, type ApiCalendarEventResponse } from '@/api-access/calendar';
import { CalendarEventTypeCode } from '@/api-access/generated/models';
import { toCalendarDateOnly } from '@/utils/date';
import type { CalendarEventBase } from '../calendarTypes';

export function mapApiCalendarEventToCalendarEventBase(apiEvent: ApiCalendarEventResponse): CalendarEventBase {
  const start = apiEvent.allDay
    ? (toCalendarDateOnly(apiEvent.startAtUtc) ?? apiEvent.startAtUtc)
    : apiEvent.startAtUtc;
  const end = apiEvent.allDay ? toCalendarDateOnly(apiEvent.endAtUtc) : apiEvent.endAtUtc;
  const eventTypeCode = apiEvent.eventTypeCode || CalendarEventTypeCode.General;

  return {
    id: String(apiEvent.id),
    type: apiEvent.type ?? calendarEventTypes.calendarEvent,
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
    locationId: apiEvent.locationId,
  };
}
