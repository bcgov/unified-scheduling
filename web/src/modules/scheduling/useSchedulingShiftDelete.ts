import { computed, ref, type ComputedRef, type Ref } from 'vue';
import type { ShiftSeriesResponse } from '@/api-access/generated/models/shiftSeriesResponse';
import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import type { ShiftOpenScope } from './calendarSchedulingShiftDetailTypes';
import * as shiftApi from './calendarSchedulingShiftApi';
import { resolveShiftEntryId, resolveShiftSeriesId } from './calendarSchedulingShiftIds';
import { normalizeSchedulingLifecycleStatus } from './schedulingLifecycle';

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
  const isCancelAction = computed(
    () =>
      normalizeSchedulingLifecycleStatus(
        options.selectedOpenScope.value === 'series'
          ? options.selectedSeries.value?.statusTypeCode
          : options.event.value.statusTypeCode,
      ) === 'published',
  );
  const canDeleteShift = computed(() => !deleteDisabledReason.value && isDeleteConfirmed.value);
  const deleteActionLabel = computed(() => (isCancelAction.value ? 'Cancel' : 'Delete'));
  const deleteConfirmationLabel = computed(() =>
    isCancelAction.value
      ? 'I understand this published shift will be cancelled for all assigned users.'
      : 'I understand this shift will be permanently deleted for all assigned users.',
  );
  const deleteWarning = computed(() =>
    isCancelAction.value ? 'This published shift will be cancelled.' : "This can't be undone.",
  );

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
      const deleted = isCancelAction.value
        ? options.selectedOpenScope.value === 'series'
          ? await cancelShiftSeries()
          : await cancelShiftEntry()
        : options.selectedOpenScope.value === 'series'
          ? await deleteShiftSeries()
          : await deleteShiftEntry();

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

  async function cancelShiftEntry() {
    const id = resolveShiftEntryId(options.event.value);
    if (!id) {
      deleteError.value = 'Could not determine the shift entry to cancel.';
      return false;
    }

    const result = await shiftApi.cancelShiftEntry(id);

    if (result.error.value) {
      deleteError.value = result.error.value.message || 'Failed to cancel shift entry.';
      return false;
    }

    return true;
  }

  async function cancelShiftSeries() {
    const id = resolveShiftSeriesId(options.event.value);
    if (!id) {
      deleteError.value = 'Could not determine the shift series to cancel.';
      return false;
    }

    const result = await shiftApi.cancelShiftSeries(id);

    if (result.error.value) {
      deleteError.value = result.error.value.message || 'Failed to cancel shift series.';
      return false;
    }

    return true;
  }

  return {
    canDeleteShift,
    deleteActionLabel,
    deleteConfirmationLabel,
    deleteDisabledReason,
    deleteError,
    deleteWarning,
    isCancelAction,
    isDeleteConfirmed,
    isDeleting,
    clearDeleteState,
    deleteShift,
  };
}

export function getShiftDeleteDisabledReason(scope: ShiftOpenScope | null, statusTypeCode?: string | null) {
  const normalizedStatus = normalizeSchedulingLifecycleStatus(statusTypeCode);

  if (!statusTypeCode?.trim() || normalizedStatus === 'draft' || normalizedStatus === 'published') {
    return '';
  }

  return scope === 'series'
    ? 'Only draft or published shift series can be deleted.'
    : 'Only draft or published shift entries can be deleted.';
}
