import { CalendarEventType, CalendarEventTypeCode, type CalendarEventResponse } from '@/api-access/generated/models';
import { toCalendarDateOnly } from '@/utils/date';
import type { CalendarEventBase } from '../calendarTypes';

export function mapApiCalendarEventToCalendarEventBase(apiEvent: CalendarEventResponse): CalendarEventBase {
  const endAtUtc = apiEvent.endAtUtc ?? undefined;
  const start = apiEvent.allDay
    ? (toCalendarDateOnly(apiEvent.startAtUtc) ?? apiEvent.startAtUtc)
    : apiEvent.startAtUtc;
  const end = apiEvent.allDay ? toCalendarDateOnly(endAtUtc) : endAtUtc;
  const eventTypeCode = apiEvent.eventTypeCode || CalendarEventTypeCode.General;

  return {
    id: String(apiEvent.id),
    type: apiEvent.type ?? CalendarEventType.calendarevent,
    sourceModule: apiEvent.sourceModule,
    title: apiEvent.title,
    description: apiEvent.description ?? undefined,
    notes: apiEvent.notes ?? undefined,
    color: apiEvent.color ?? undefined,
    eventSeriesId: apiEvent.eventSeriesId ?? undefined,
    start,
    end,
    seriesStartAtUtc: apiEvent.seriesStartAtUtc ?? undefined,
    seriesEndAtUtc: apiEvent.seriesEndAtUtc ?? undefined,
    allDay: apiEvent.allDay,
    isReadOnly: apiEvent.isReadOnly,
    isException: apiEvent.isException,
    holidayType: apiEvent.holidayType ?? undefined,
    eventTypeCode,
    statusTypeCode: apiEvent.statusTypeCode,
    cancelledAt: apiEvent.cancelledAt ?? undefined,
    cancelledByUserId: apiEvent.cancelledByUserId ?? undefined,
    cancellationReason: apiEvent.cancellationReason ?? undefined,
    timeZoneId: apiEvent.timeZoneId ?? undefined,
    locationId: apiEvent.locationId ?? undefined,
  };
}
