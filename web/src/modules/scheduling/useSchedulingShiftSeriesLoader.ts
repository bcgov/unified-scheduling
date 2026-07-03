import { ref, type ComputedRef } from 'vue';
import type { ShiftSeriesResponse } from '@/api-access/generated/models/shiftSeriesResponse';
import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import * as shiftApi from './calendarSchedulingShiftApi';
import { resolveShiftSeriesId } from './calendarSchedulingShiftIds';

export function useSchedulingShiftSeriesLoader(options: {
  event: ComputedRef<CalendarEventBase>;
  onError: (message: string) => void;
}) {
  const selectedSeries = ref<ShiftSeriesResponse | null>(null);
  const isLoadingSeries = ref(false);

  async function loadSelectedSeries() {
    const id = resolveShiftSeriesId(options.event.value);
    if (!id) {
      options.onError('Could not determine the shift series to open.');
      return null;
    }

    isLoadingSeries.value = true;

    try {
      const result = await shiftApi.loadShiftSeries(id);

      if (result.error.value) {
        options.onError(result.error.value.message || 'Failed to load shift series.');
        return null;
      }

      selectedSeries.value = result.data.value ?? null;
      if (!selectedSeries.value) {
        options.onError('Shift series was not found.');
      }

      return selectedSeries.value;
    } finally {
      isLoadingSeries.value = false;
    }
  }

  function clearSelectedSeries() {
    selectedSeries.value = null;
  }

  return {
    selectedSeries,
    isLoadingSeries,
    loadSelectedSeries,
    clearSelectedSeries,
  };
}
