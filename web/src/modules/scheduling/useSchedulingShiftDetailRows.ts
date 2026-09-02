import { computed, type ComputedRef, type Ref } from 'vue';
import type { ShiftSeriesResponse } from '@/api-access/generated/models/shiftSeriesResponse';
import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import type { SelectOption } from '@/types/select';
import { formatCalendarDateOnly } from '@/utils/date';
import { resolveCalendarEventUserIds } from './calendarSchedulingEventUsers';
import { formatAssigneeIds } from './calendarSchedulingShiftDetailRows';
import { publishOptions, type ShiftResourceFormData } from './calendarSchedulingShiftForm';
import { parsePositiveInteger } from './calendarSchedulingShiftIds';
import type { ShiftDetailRow, ShiftOpenScope } from './calendarSchedulingShiftDetailTypes';
import { formatTimeOptionRange } from './schedulingDateTime';
import { normalizeSchedulingLifecycleStatus } from './schedulingLifecycle';

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
}) {
  const detailRows = computed<ShiftDetailRow[]>(() => {
    const event = options.event.value;
    const formData = options.formData.value;
    const lifecycleStatus = normalizeSchedulingLifecycleStatus(formData.statusTypeCode);
    const rows: ShiftDetailRow[] = [
      {
        label: 'Location',
        value: formatLocation(formData.locationId ?? event.locationId, options.locationOptions.value),
      },
      {
        label: 'Employee',
        value: formatAssigneeIds(formData.userIds ?? resolveCalendarEventUserIds(event), options.employeeOptions.value),
      },
      { label: 'Date', value: formData.date ? formatCalendarDateOnly(formData.date) || 'Unknown' : 'Unknown' },
      {
        label: 'Time',
        value: formatTimeOptionRange(formData.startTime, formData.endTime),
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

    if (lifecycleStatus === 'draft') {
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
  const parsedLocationId = parsePositiveInteger(locationId);
  if (!parsedLocationId) {
    return 'Unknown location';
  }

  const option = locationOptions.find((candidate) => Number(candidate.code) === parsedLocationId);
  return option?.description || 'Unknown location';
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
      const id = parsePositiveInteger(link[idKey]);
      const assignmentName = formatAssignmentName(id, assignmentOptions, `${fallbackLabel} ${index + 1}`);
      let rawUserIds: unknown[] = [];
      if ('assignedUserIds' in link && Array.isArray(link.assignedUserIds)) {
        rawUserIds = link.assignedUserIds;
      } else if ('userIds' in link && Array.isArray(link.userIds)) {
        rawUserIds = link.userIds;
      }
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
