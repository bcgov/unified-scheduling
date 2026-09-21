import { ref, type ComputedRef } from 'vue';
import { getApiSchedulingShiftsEntries, getApiSchedulingShiftsSeries } from '@/api-access/generated/shift/shift';
import type { ShiftEntryResponse, ShiftSeriesResponse } from '@/api-access/generated/models';
import { createLatestRequestGuard } from './latestRequestGuard';
import { toUtcCalendarDayRange } from './schedulingDateTime';
import type { ShiftEntryLinkFormData, ShiftSeriesLinkFormData } from './calendarSchedulingAssignmentForm';

export interface SchedulingAssignmentShiftOptionsOptions {
  locationId: ComputedRef<number | null>;
  date: ComputedRef<string | undefined>;
  timeZoneId: ComputedRef<string>;
  onError(message: string): void;
  onLoaded?(): void;
}

export function useSchedulingAssignmentShiftOptions(options: SchedulingAssignmentShiftOptionsOptions) {
  const shiftSeries = ref<ShiftSeriesResponse[]>([]);
  const shiftEntries = ref<ShiftEntryResponse[]>([]);
  const isLoading = ref(false);
  const requestGuard = createLatestRequestGuard();

  async function load() {
    const requestId = requestGuard.begin();
    shiftSeries.value = [];
    shiftEntries.value = [];
    if (!options.locationId.value) {
      isLoading.value = false;
      return;
    }

    isLoading.value = true;
    try {
      const dateRange = toUtcCalendarDayRange(options.date.value, options.timeZoneId.value);
      const params = {
        LocationId: options.locationId.value,
        ...(dateRange ? { StartAtUtc: dateRange.startAtUtc, EndAtUtc: dateRange.endAtUtc } : {}),
      };
      const seriesResult = getApiSchedulingShiftsSeries(params, { options: { immediate: false } });
      const entryResult = getApiSchedulingShiftsEntries(params, { options: { immediate: false } });
      await Promise.all([seriesResult.execute(), entryResult.execute()]);

      if (!requestGuard.isCurrent(requestId)) {
        return;
      }
      if (seriesResult.error.value || entryResult.error.value) {
        options.onError(
          seriesResult.error.value?.message || entryResult.error.value?.message || 'Failed to load shift options.',
        );
        return;
      }

      shiftSeries.value = seriesResult.data.value ?? [];
      shiftEntries.value = entryResult.data.value ?? [];
      options.onLoaded?.();
    } catch (error: unknown) {
      if (requestGuard.isCurrent(requestId)) {
        options.onError(error instanceof Error ? error.message : 'Failed to load shift options.');
      }
    } finally {
      if (requestGuard.isCurrent(requestId)) {
        isLoading.value = false;
      }
    }
  }

  return { isLoading, load, shiftEntries, shiftSeries };
}

export function promoteAssignmentShiftEntryLinks(
  entryLinks: readonly ShiftEntryLinkFormData[],
  seriesLinks: readonly ShiftSeriesLinkFormData[],
  entries: readonly ShiftEntryResponse[],
  series: readonly ShiftSeriesResponse[],
) {
  const links = new Map(seriesLinks.map((link) => [link.shiftSeriesId, { ...link }]));
  for (const entryLink of entryLinks) {
    const shiftSeriesId = Number(entries.find((entry) => entry.id === entryLink.shiftEntryId)?.shiftSeriesId);
    if (!Number.isInteger(shiftSeriesId) || shiftSeriesId <= 0 || links.has(shiftSeriesId)) {
      continue;
    }
    const assignedUserIds = series.find((item) => item.id === shiftSeriesId)?.userIds ?? [];
    links.set(shiftSeriesId, {
      shiftSeriesId,
      assignedUserIds: assignedUserIds.length ? assignedUserIds : entryLink.assignedUserIds,
    });
  }
  return [...links.values()];
}
