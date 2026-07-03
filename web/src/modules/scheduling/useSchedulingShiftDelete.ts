import { computed, ref, type ComputedRef, type Ref } from 'vue';
import type { ShiftSeriesResponse } from '@/api-access/generated/models/shiftSeriesResponse';
import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import type { ShiftOpenScope } from './calendarSchedulingShiftDetailTypes';
import * as shiftApi from './calendarSchedulingShiftApi';
import { resolveShiftEntryId, resolveShiftSeriesId } from './calendarSchedulingShiftIds';

export function useSchedulingShiftDelete(options: {
  event: ComputedRef<CalendarEventBase>;
  selectedOpenScope: Ref<ShiftOpenScope | null>;
  selectedSeries: Ref<ShiftSeriesResponse | null>;
}) {
  const isDeleting = ref(false);
  const deleteError = ref('');
  const isDeleteConfirmed = ref(false);

  const deleteDisabledReason = computed(() =>
    getShiftDeleteDisabledReason(
      options.selectedOpenScope.value,
      options.selectedOpenScope.value === 'series'
        ? options.selectedSeries.value?.statusTypeCode
        : options.event.value.statusTypeCode,
    ),
  );
  const canDeleteShift = computed(() => !deleteDisabledReason.value && isDeleteConfirmed.value);

  function clearDeleteState() {
    deleteError.value = '';
    isDeleteConfirmed.value = false;
  }

  async function deleteShift() {
    if (!canDeleteShift.value) {
      return false;
    }

    isDeleting.value = true;
    deleteError.value = '';

    try {
      const deleted =
        options.selectedOpenScope.value === 'series' ? await deleteShiftSeries() : await deleteShiftEntry();

      return deleted;
    } catch (error: unknown) {
      deleteError.value = error instanceof Error ? error.message : 'An unexpected error occurred.';
      return false;
    } finally {
      isDeleting.value = false;
    }
  }

  async function deleteShiftEntry() {
    const id = resolveShiftEntryId(options.event.value);
    if (!id) {
      deleteError.value = 'Could not determine the shift entry to delete.';
      return false;
    }

    const result = await shiftApi.deleteShiftEntry(id);

    if (result.error.value) {
      deleteError.value = result.error.value.message || 'Failed to delete shift entry.';
      return false;
    }

    return true;
  }

  async function deleteShiftSeries() {
    const id = resolveShiftSeriesId(options.event.value);
    if (!id) {
      deleteError.value = 'Could not determine the shift to delete.';
      return false;
    }

    const result = await shiftApi.deleteShiftSeries(id);

    if (result.error.value) {
      deleteError.value = result.error.value.message || 'Failed to delete shift series.';
      return false;
    }

    return true;
  }

  return {
    canDeleteShift,
    deleteDisabledReason,
    deleteError,
    isDeleteConfirmed,
    isDeleting,
    clearDeleteState,
    deleteShift,
  };
}

export function getShiftDeleteDisabledReason(scope: ShiftOpenScope | null, statusTypeCode?: string | null) {
  const normalizedStatus = String(statusTypeCode ?? '').toLowerCase();

  if (normalizedStatus && normalizedStatus !== 'draft') {
    return scope === 'series' ? 'Only draft shift series can be deleted.' : 'Only draft shift entries can be deleted.';
  }

  return '';
}
