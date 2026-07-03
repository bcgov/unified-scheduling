import { computed, type ComputedRef, type Ref } from 'vue';
import type { ShiftSeriesResponse } from '@/api-access/generated/models/shiftSeriesResponse';
import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import { formatCalendarEventTimeRange, toDateTime } from '@/utils/date';
import type { SelectOption } from '@/types/select';
import { isCalendarSchedulingEvent } from './calendarSchedulingData';
import type { ShiftDetailRow, ShiftOpenScope } from './calendarSchedulingShiftDetailTypes';

export function useSchedulingShiftDetailRows(options: {
  event: ComputedRef<CalendarEventBase>;
  selectedOpenScope: Ref<ShiftOpenScope | null>;
  selectedSeries: Ref<ShiftSeriesResponse | null>;
  employeeOptions: ComputedRef<SelectOption[]>;
  activeTimeZoneId: ComputedRef<string>;
}) {
  const detailRows = computed<ShiftDetailRow[]>(() => {
    const event = options.event.value;

    if (options.selectedOpenScope.value === 'series' && options.selectedSeries.value) {
      const series = options.selectedSeries.value;

      return [
        { label: 'Assignee(s)', value: formatAssigneeIds(series.userIds ?? [], options.employeeOptions.value) },
        {
          label: 'Date',
          value: formatEventDate(series.startAtUtc ?? event.start, options.activeTimeZoneId.value),
        },
        {
          label: 'Time',
          value: formatCalendarEventTimeRange(series.startAtUtc ?? event.start, series.endAtUtc ?? event.end, {
            allDay: series.allDay ?? false,
            timeZone: options.activeTimeZoneId.value,
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
      { label: 'Assignee(s)', value: formatAssigneeIds(getEventUserIds(event), options.employeeOptions.value) },
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
  });

  return {
    detailRows,
  };
}

function formatEventDate(value: string, timeZone?: string) {
  const dateTime = toDateTime(value, timeZone);

  if (!dateTime.isValid) {
    return 'Unknown';
  }

  return dateTime.toFormat('LLLL d, yyyy');
}

function formatAssigneeIds(userIds: string[], employeeOptions: SelectOption[]) {
  if (userIds.length === 0) {
    return 'None';
  }

  return userIds.map((userId) => formatAssignee(userId, employeeOptions)).join(', ');
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

function formatAssignee(userId: string, employeeOptions: SelectOption[]) {
  const option = employeeOptions.find((candidate) => String(candidate.code) === userId);
  return option?.description || userId;
}
