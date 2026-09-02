import type { AssignmentEntryResponse } from '@/api-access/generated/models/assignmentEntryResponse';
import type { AssignmentSeriesResponse } from '@/api-access/generated/models/assignmentSeriesResponse';
import type { SelectOption } from '@/types/select';
import { DateTime } from 'luxon';
import { computed, ref, watch, type ComputedRef, type Ref } from 'vue';
import type { ShiftResourceFormData } from './calendarSchedulingShiftForm';
import * as assignmentApi from './calendarSchedulingAssignmentApi';
import { expandSchedulingRecurrence, type SchedulingOccurrence } from './schedulingRecurrence';
import { isSchedulingLinkable } from './schedulingLifecycle';
import { createLatestRequestGuard } from './latestRequestGuard';

type ShiftOccurrence = SchedulingOccurrence;

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
    const occurrences = buildShiftOccurrences(options.formData.value, timeZoneId, isSeriesScope);

    assignmentEntries.value = [];
    assignmentSeries.value = [];
    assignmentWarning.value = '';

    if (!locationId || occurrences.length === 0) {
      isLoadingAssignments.value = false;
      return;
    }

    isLoadingAssignments.value = true;

    try {
      const entryRange = buildWholeDayRange([occurrences[0]], timeZoneId);
      if (entryRange) {
        const entryResult = await assignmentApi.loadAssignmentEntries({
          LocationId: locationId,
          StartAtUtc: entryRange.startAtUtc,
          EndAtUtc: entryRange.endAtUtc,
        });

        if (!requestGuard.isCurrent(requestId)) {
          return;
        }

        if (entryResult.error.value) {
          throw new Error(entryResult.error.value.message || 'Failed to load assignments.');
        }
        assignmentEntries.value = (entryResult.data.value ?? []).filter((entry) =>
          isSchedulingLinkable(entry.statusTypeCode),
        );
      }

      if (isSeriesScope) {
        const seriesRange = buildWholeDayRange(occurrences, timeZoneId);
        if (seriesRange) {
          const seriesResult = await assignmentApi.loadAssignmentSeries({
            LocationId: locationId,
            StartAtUtc: seriesRange.startAtUtc,
            EndAtUtc: seriesRange.endAtUtc,
          });

          if (!requestGuard.isCurrent(requestId)) {
            return;
          }

          if (seriesResult.error.value) {
            throw new Error(seriesResult.error.value.message || 'Failed to load assignment series.');
          }
          assignmentSeries.value = filterSeriesByMatchingActiveEntryDate(
            (seriesResult.data.value ?? []).filter((series) => isSchedulingLinkable(series.statusTypeCode)),
            occurrences,
            timeZoneId,
          );
        }
      }

      if (!requestGuard.isCurrent(requestId)) {
        return;
      }

      assignmentWarning.value = buildNoTimeOverlapWarning(
        assignmentEntries.value,
        assignmentSeries.value,
        occurrences,
        timeZoneId,
      );
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

function buildShiftOccurrences(
  formData: ShiftResourceFormData,
  timeZoneId: string,
  isSeriesScope: boolean,
): ShiftOccurrence[] {
  const first = buildOccurrence(formData.date, formData.startTime, formData.endTime, timeZoneId);
  if (!first) {
    return [];
  }

  if (!isSeriesScope || !formData.recurrenceRule) {
    return [first];
  }

  return expandSchedulingRecurrence(first.start, first.end.diff(first.start), formData.recurrenceRule);
}

function buildOccurrence(
  date: string | undefined,
  startTime: string | undefined,
  endTime: string | undefined,
  timeZoneId: string,
) {
  if (!date || !startTime || !endTime) {
    return null;
  }

  const start = DateTime.fromISO(`${date}T${startTime}`, { zone: timeZoneId });
  const end = DateTime.fromISO(`${date}T${endTime}`, { zone: timeZoneId });
  if (!start.isValid || !end.isValid || end <= start) {
    return null;
  }

  return {
    start,
    end,
    dateKey: start.toISODate() ?? '',
  };
}

function buildWholeDayRange(occurrences: ShiftOccurrence[], timeZoneId: string) {
  const dateKeys = occurrences
    .map((occurrence) => occurrence.dateKey)
    .filter(Boolean)
    .sort();
  const firstDate = dateKeys[0];
  const lastDate = dateKeys.at(-1);
  if (!firstDate || !lastDate) {
    return null;
  }

  return {
    startAtUtc:
      DateTime.fromISO(firstDate, { zone: timeZoneId }).startOf('day').toUTC().toISO({ suppressMilliseconds: true }) ??
      '',
    endAtUtc:
      DateTime.fromISO(lastDate, { zone: timeZoneId })
        .plus({ days: 1 })
        .startOf('day')
        .toUTC()
        .toISO({ suppressMilliseconds: true }) ?? '',
  };
}

function filterSeriesByMatchingActiveEntryDate(
  series: AssignmentSeriesResponse[],
  occurrences: ShiftOccurrence[],
  timeZoneId: string,
) {
  return series.filter((item) =>
    (item.entries ?? []).some(
      (entry) =>
        isSchedulingLinkable(entry.statusTypeCode) &&
        occurrences.some((occurrence) => entryOverlapsShiftDate(entry, occurrence, timeZoneId)),
    ),
  );
}

function buildNoTimeOverlapWarning(
  entries: AssignmentEntryResponse[],
  series: AssignmentSeriesResponse[],
  occurrences: ShiftOccurrence[],
  timeZoneId: string,
) {
  const hasEntryDateOnlyMatch = entries.some((entry) => hasDateMatchWithoutTimeOverlap(entry, occurrences, timeZoneId));
  const hasSeriesDateOnlyMatch = series.some((item) =>
    (item.entries ?? []).some(
      (entry) =>
        isSchedulingLinkable(entry.statusTypeCode) && hasDateMatchWithoutTimeOverlap(entry, occurrences, timeZoneId),
    ),
  );

  if (!hasEntryDateOnlyMatch && !hasSeriesDateOnlyMatch) {
    return '';
  }

  return 'One or more matching assignments occur on the same day but do not overlap this shift time.';
}

function hasDateMatchWithoutTimeOverlap(
  entry: AssignmentEntryResponse,
  occurrences: ShiftOccurrence[],
  timeZoneId: string,
) {
  const interval = getEntryInterval(entry, timeZoneId);
  if (!interval) {
    return false;
  }

  const dateMatches = occurrences.filter((occurrence) =>
    intervalsOverlap(
      occurrence.start.startOf('day'),
      occurrence.start.plus({ days: 1 }).startOf('day'),
      interval.start,
      interval.end,
    ),
  );
  return (
    dateMatches.length > 0 &&
    dateMatches.every((occurrence) => !intervalsOverlap(occurrence.start, occurrence.end, interval.start, interval.end))
  );
}

function entryOverlapsShiftDate(entry: AssignmentEntryResponse, occurrence: ShiftOccurrence, timeZoneId: string) {
  const interval = getEntryInterval(entry, timeZoneId);
  if (!interval) {
    return false;
  }

  return intervalsOverlap(
    occurrence.start.startOf('day'),
    occurrence.start.plus({ days: 1 }).startOf('day'),
    interval.start,
    interval.end,
  );
}

function getEntryInterval(entry: AssignmentEntryResponse, timeZoneId: string) {
  if (!entry.startAtUtc) {
    return null;
  }

  const start = DateTime.fromISO(entry.startAtUtc, { zone: timeZoneId });
  const end = entry.endAtUtc ? DateTime.fromISO(entry.endAtUtc, { zone: timeZoneId }) : start;
  if (!start.isValid || !end.isValid) {
    return null;
  }

  return { start, end };
}

function intervalsOverlap(leftStart: DateTime, leftEnd: DateTime, rightStart: DateTime, rightEnd: DateTime) {
  return leftStart < rightEnd && rightStart < leftEnd;
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
