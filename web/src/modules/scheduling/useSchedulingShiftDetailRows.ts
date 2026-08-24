import { computed, type ComputedRef, type Ref } from 'vue';
import type { ShiftSeriesResponse } from '@/api-access/generated/models/shiftSeriesResponse';
import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import type { SelectOption } from '@/types/select';
import { isCalendarSchedulingEvent } from './calendarSchedulingData';
import { publishOptions, timeOptions, type ShiftResourceFormData } from './calendarSchedulingShiftForm';
import type { ShiftDetailRow, ShiftOpenScope } from './calendarSchedulingShiftDetailTypes';

type AssignmentDetailLink = {
  assignmentEntryId?: number;
  assignmentSeriesId?: number | null;
  userIds?: string[] | null;
};

export function useSchedulingShiftDetailRows(options: {
  event: ComputedRef<CalendarEventBase>;
  selectedOpenScope: Ref<ShiftOpenScope | null>;
  selectedSeries: Ref<ShiftSeriesResponse | null>;
  formData: Ref<ShiftResourceFormData>;
  employeeOptions: ComputedRef<SelectOption[]>;
  assignmentEntryOptions: ComputedRef<SelectOption[]>;
  assignmentSeriesOptions: ComputedRef<SelectOption[]>;
  locationOptions: ComputedRef<SelectOption[]>;
  activeTimeZoneId: ComputedRef<string>;
}) {
  const detailRows = computed<ShiftDetailRow[]>(() => {
    const event = options.event.value;
    const formData = options.formData.value;
    const statusTypeCode = String(formData.statusTypeCode ?? '').toLowerCase();
    const rows: ShiftDetailRow[] = [
      {
        label: 'Location',
        value: formatLocation(formData.locationId ?? event.locationId, options.locationOptions.value),
      },
      {
        label: 'Employee',
        value: formatAssigneeIds(formData.userIds ?? getEventUserIds(event), options.employeeOptions.value),
      },
      { label: 'Date', value: formatFormDate(formData.date) },
      {
        label: 'Time',
        value: formatFormTimeRange(formData.startTime, formData.endTime),
      },
    ];

    if (options.selectedOpenScope.value === 'series' && options.selectedSeries.value) {
      const series = options.selectedSeries.value;
      rows.push(
        {
          label: 'Repeat',
          value: '',
          recurrenceRule: formData.recurrenceRule ?? series.recurrenceRule ?? null,
          recurrenceStartDate: series.startAtUtc ?? event.start,
        },
        {
          label: 'Series Assignments',
          value: formatAssignmentLinks(
            formData.assignmentSeriesLinks ?? [],
            options.assignmentSeriesOptions.value,
            options.employeeOptions.value,
            'Assignment series',
            'assignmentSeriesId',
          ),
        },
      );
    } else {
      rows.push({
        label: 'Assignment(s)',
        value: formatAssignmentLinks(
          formData.assignmentEntryLinks ?? [],
          options.assignmentEntryOptions.value,
          options.employeeOptions.value,
          'Assignment',
          'assignmentEntryId',
        ),
      });
    }

    rows.push({ label: 'Training', value: formData.trainingLabel?.trim() || 'None' });

    if (statusTypeCode === 'draft') {
      rows.push({ label: 'Publish', value: formatSelectValue(formData.publish, publishOptions) });
    }

    rows.push({ label: 'Notes', value: formData.notes?.trim() || 'None' });

    return rows;
  });

  return {
    detailRows,
  };
}

function formatLocation(locationId: unknown, locationOptions: SelectOption[]) {
  const parsedLocationId = parsePositiveNumber(locationId);
  if (!parsedLocationId) {
    return 'Unknown location';
  }

  const option = locationOptions.find((candidate) => Number(candidate.code) === parsedLocationId);
  return option?.description || 'Unknown location';
}

function formatFormDate(value?: string) {
  if (!value) {
    return 'Unknown';
  }

  const dateTime = new Date(`${value}T00:00:00`);

  if (Number.isNaN(dateTime.getTime())) {
    return 'Unknown';
  }

  return new Intl.DateTimeFormat(undefined, {
    month: 'long',
    day: 'numeric',
    year: 'numeric',
  }).format(dateTime);
}

function formatFormTimeRange(startTime?: string, endTime?: string) {
  const start = formatTimeValue(startTime);
  const end = formatTimeValue(endTime);

  if (!start && !end) {
    return 'Unknown';
  }

  if (!end) {
    return start;
  }

  if (!start) {
    return end;
  }

  return `${start} - ${end}`;
}

function formatTimeValue(value?: string) {
  if (!value) {
    return '';
  }

  const option = timeOptions.find((candidate) => candidate.code === value);
  if (option) {
    return option.description;
  }

  const timeMatch = /^(\d{1,2}):(\d{2})(?::\d{2}(?:\.\d+)?)?$/.exec(value.trim());
  if (!timeMatch) {
    return value;
  }

  const hour = Number(timeMatch[1]);
  const minute = Number(timeMatch[2]);
  const matchedOption = timeOptions.find(
    (candidate) => candidate.code === `${String(hour).padStart(2, '0')}:${timeMatch[2]}`,
  );
  if (matchedOption) {
    return matchedOption.description;
  }

  return new Intl.DateTimeFormat(undefined, {
    hour: 'numeric',
    minute: '2-digit',
  }).format(new Date(2000, 0, 1, hour, minute));
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

function formatAssignmentLinks(
  links: AssignmentDetailLink[],
  assignmentOptions: SelectOption[],
  employeeOptions: SelectOption[],
  fallbackLabel: string,
  idKey: 'assignmentEntryId' | 'assignmentSeriesId',
) {
  if (links.length === 0) {
    return 'None';
  }

  return links
    .map((link, index) => {
      const id = parsePositiveNumber(link[idKey]);
      const assignmentName = formatAssignmentName(id, assignmentOptions, `${fallbackLabel} ${index + 1}`);
      const rawUserIds =
        'assignedUserIds' in link && Array.isArray(link.assignedUserIds)
          ? link.assignedUserIds
          : 'userIds' in link && Array.isArray(link.userIds)
            ? link.userIds
            : [];
      const userIds = rawUserIds.filter((userId): userId is string => typeof userId === 'string');
      const users = formatAssigneeIds(userIds, employeeOptions);

      return `${assignmentName} — Users: ${users}`;
    })
    .join('\n');
}

function formatAssignmentName(id: number | null, assignmentOptions: SelectOption[], fallback: string) {
  if (!id) {
    return fallback;
  }

  const option = assignmentOptions.find((candidate) => Number(candidate.code) === id);
  return option?.description || `${fallback} ${id}`;
}

function formatSelectValue(value: unknown, options: SelectOption[]) {
  const option = options.find((candidate) => candidate.code === value);
  return option?.description ?? 'No';
}

function parsePositiveNumber(value: unknown) {
  const parsed = typeof value === 'number' ? value : typeof value === 'string' ? Number(value) : NaN;
  return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}
