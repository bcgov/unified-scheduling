import type { ShiftSeriesResponse } from '@/api-access/generated/models/shiftSeriesResponse';
import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import { formatCalendarEventTimeRange, toDateTime } from '@/utils/date';
import type { SelectOption } from '@/types/select';
import { isCalendarSchedulingEvent } from './calendarSchedulingData';

export type ShiftDetailRow = {
  label: string;
  value: string;
  recurrenceRule?: string | null;
  recurrenceStartDate?: string | null;
};

interface CreateShiftDetailRowsOptions {
  event: CalendarEventBase;
  series: ShiftSeriesResponse | null;
  timeZoneId: string;
  employeeOptions: SelectOption[];
}

export function createShiftDetailRows({
  event,
  series,
  timeZoneId,
  employeeOptions,
}: CreateShiftDetailRowsOptions): ShiftDetailRow[] {
  if (series) {
    return [
      { label: 'Assignee(s)', value: formatAssigneeIds(series.userIds ?? [], employeeOptions) },
      { label: 'Date', value: formatEventDate(series.startAtUtc ?? event.start, timeZoneId) },
      {
        label: 'Time',
        value: formatCalendarEventTimeRange(series.startAtUtc ?? event.start, series.endAtUtc ?? event.end, {
          allDay: series.allDay ?? false,
          timeZone: timeZoneId,
        }),
      },
      { label: 'Notes', value: series.notes?.trim() || 'None' },
      {
        label: 'Repeat',
        value: '',
        recurrenceRule: series.recurrenceRule ?? null,
        recurrenceStartDate: series.startAtUtc ?? event.start,
      },
    ];
  }

  return [
    { label: 'Assignee(s)', value: formatAssigneeIds(getEventUserIds(event), employeeOptions) },
    { label: 'Date', value: formatEventDate(event.start, event.timeZoneId) },
    {
      label: 'Time',
      value: formatCalendarEventTimeRange(event.start, event.end, {
        allDay: event.allDay,
        timeZone: event.timeZoneId,
      }),
    },
    { label: 'Notes', value: event.notes?.trim() || 'None' },
  ];
}

function formatEventDate(value: string, timeZone?: string) {
  const dateTime = toDateTime(value, timeZone);

  return dateTime.isValid ? dateTime.toFormat('LLLL d, yyyy') : 'Unknown';
}

function formatAssigneeIds(userIds: string[], employeeOptions: SelectOption[]) {
  if (userIds.length === 0) {
    return 'None';
  }

  return userIds
    .map((userId) => employeeOptions.find((option) => String(option.code) === userId)?.description || userId)
    .join(', ');
}

function getEventUserIds(event: CalendarEventBase) {
  if (!isCalendarSchedulingEvent(event)) {
    return event.resourceIds ?? [];
  }

  if (event.metadata.userIds?.length) {
    return event.metadata.userIds;
  }

  return event.metadata.userId ? [event.metadata.userId] : (event.resourceIds ?? []);
}
