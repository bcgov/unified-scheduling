import type { AssignmentEntryResponse } from '@/api-access/generated/models/assignmentEntryResponse';
import type { AssignmentSeriesResponse } from '@/api-access/generated/models/assignmentSeriesResponse';
import { postApiSchedulingShiftAssignmentsOptions } from '@/api-access/generated/shift-assignment/shift-assignment';
import type { SelectOption } from '@/types/select';
import { DateTime } from 'luxon';
import { computed, ref, watch, type ComputedRef, type Ref } from 'vue';
import type { ShiftResourceFormData } from './calendarSchedulingShiftForm';
import { createLatestRequestGuard } from './latestRequestGuard';
import { toUtcIso } from './schedulingDateTime';

const noTimeOverlapWarning =
  'One or more matching assignments occur on the same day but do not overlap this shift time.';

export function useSchedulingAssignmentOptions(options: {
  formData: Ref<ShiftResourceFormData>;
  activeLocationId: ComputedRef<number | null>;
  activeTimeZoneId: ComputedRef<string>;
  isSeriesScope: ComputedRef<boolean>;
  onError: (message: string) => void;
}) {
  const assignmentEntries = ref<AssignmentEntryResponse[]>([]);
  const assignmentSeries = ref<AssignmentSeriesResponse[]>([]);
  const isLoadingAssignments = ref(false);
  const assignmentWarning = ref('');
  const requestGuard = createLatestRequestGuard();

  const assignmentEntryOptions = computed<SelectOption[]>(() =>
    assignmentEntries.value.filter(hasNumericId).map((entry) => ({
      code: entry.id!,
      description: formatAssignmentEntryLabel(entry, options.activeTimeZoneId.value),
    })),
  );

  const assignmentSeriesOptions = computed<SelectOption[]>(() =>
    assignmentSeries.value.filter(hasNumericId).map((series) => ({
      code: series.id!,
      description: formatAssignmentSeriesLabel(series, options.activeTimeZoneId.value),
    })),
  );

  watch(
    [
      () => options.formData.value.date,
      () => options.formData.value.startTime,
      () => options.formData.value.endTime,
      () => options.formData.value.recurrenceRule,
      () => options.formData.value.repeatMode,
      () => options.activeLocationId.value,
      () => options.activeTimeZoneId.value,
      () => options.isSeriesScope.value,
    ],
    () => {
      void loadOptions();
    },
    { immediate: true },
  );

  async function loadOptions() {
    const requestId = requestGuard.begin();
    const locationId = options.activeLocationId.value;
    const timeZoneId = options.activeTimeZoneId.value;
    const isSeriesScope = options.isSeriesScope.value;
    const startAtUtc = toUtcIso(options.formData.value.date, options.formData.value.startTime, timeZoneId);
    const endAtUtc = toUtcIso(options.formData.value.date, options.formData.value.endTime, timeZoneId);

    assignmentEntries.value = [];
    assignmentSeries.value = [];
    assignmentWarning.value = '';

    if (!locationId || !startAtUtc || !endAtUtc) {
      isLoadingAssignments.value = false;
      return;
    }

    isLoadingAssignments.value = true;

    try {
      const result = postApiSchedulingShiftAssignmentsOptions(
        {
          locationId,
          startAtUtc,
          endAtUtc,
          timeZoneId,
          recurrenceRule: options.formData.value.recurrenceRule ?? null,
          isSeriesScope,
        },
        { options: { immediate: false } },
      );
      await result.execute();

      if (!requestGuard.isCurrent(requestId)) {
        return;
      }

      if (result.error.value) {
        throw new Error(result.error.value.message || 'Failed to load assignments.');
      }

      const response = result.data.value;
      if (!result.response.value?.ok || !response) {
        throw new Error('Failed to load assignments.');
      }

      assignmentEntries.value = response.entryOptions ?? [];
      assignmentSeries.value = response.seriesOptions ?? [];
      assignmentWarning.value = response.hasSameDayNonOverlappingAssignments ? noTimeOverlapWarning : '';
    } catch (error: unknown) {
      if (!requestGuard.isCurrent(requestId)) {
        return;
      }

      assignmentEntries.value = [];
      assignmentSeries.value = [];
      assignmentWarning.value = '';
      options.onError(error instanceof Error ? error.message : 'Failed to load assignments.');
    } finally {
      if (requestGuard.isCurrent(requestId)) {
        isLoadingAssignments.value = false;
      }
    }
  }

  return {
    assignmentEntryOptions,
    assignmentSeriesOptions,
    assignmentWarning,
    isLoadingAssignments,
    loadOptions,
  };
}

function hasNumericId(item: { id?: number }): item is { id: number } {
  return typeof item.id === 'number';
}

function formatAssignmentEntryLabel(entry: AssignmentEntryResponse, timeZoneId: string) {
  const title = entry.title?.trim() || `Assignment ${entry.id}`;
  const start = entry.startAtUtc ? DateTime.fromISO(entry.startAtUtc, { zone: timeZoneId }) : null;
  const end = entry.endAtUtc ? DateTime.fromISO(entry.endAtUtc, { zone: timeZoneId }) : null;
  if (!start?.isValid) {
    return title;
  }

  const time = end?.isValid
    ? `${start.toFormat('MMM d, h:mm a')} - ${end.toFormat('h:mm a')}`
    : start.toFormat('MMM d, h:mm a');
  return `${title} (${time})`;
}

function formatAssignmentSeriesLabel(series: AssignmentSeriesResponse, timeZoneId: string) {
  const title = series.title?.trim() || `Assignment series ${series.id}`;
  const start = series.startAtUtc ? DateTime.fromISO(series.startAtUtc, { zone: timeZoneId }) : null;
  const end = series.endAtUtc ? DateTime.fromISO(series.endAtUtc, { zone: timeZoneId }) : null;
  if (!start?.isValid) {
    return title;
  }

  const time = end?.isValid
    ? `${start.toFormat('MMM d, h:mm a')} - ${end.toFormat('h:mm a')}`
    : start.toFormat('MMM d, h:mm a');
  return `${title} (${time})`;
}
