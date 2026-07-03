import { ref, type ComputedRef, type Ref } from 'vue';
import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import { mapToValidationErrors } from '@/shared/validation/validationErrors';
import {
  buildUpdateShiftPayload,
  normalizeShiftFormTimes,
  validateShiftFormData,
  type ShiftResourceFormData,
} from './calendarSchedulingShiftForm';
import type { ShiftOpenScope } from './calendarSchedulingShiftDetailTypes';
import * as shiftApi from './calendarSchedulingShiftApi';
import { resolveShiftEntryId, resolveShiftSeriesId } from './calendarSchedulingShiftIds';

export function useSchedulingShiftMutation(options: {
  event: ComputedRef<CalendarEventBase>;
  formData: Ref<ShiftResourceFormData>;
  selectedOpenScope: Ref<ShiftOpenScope | null>;
  activeTimeZoneId: ComputedRef<string>;
  activeLocationId: ComputedRef<number | null>;
  existingRecurrenceRule: ComputedRef<string | null>;
}) {
  const isSaving = ref(false);
  const apiError = ref('');
  const formErrors = ref<Record<string, string>>({});
  const recurrenceError = ref('');

  function clearErrors() {
    apiError.value = '';
    formErrors.value = {};
    recurrenceError.value = '';
  }

  function handleRecurrenceInvalid(reason: string) {
    recurrenceError.value = reason;
  }

  function handleRecurrenceChange(value: string | null) {
    recurrenceError.value = '';
    options.formData.value.recurrenceRule = value;
  }

  function validateForm(): ShiftResourceFormData | null {
    formErrors.value = {};
    options.formData.value = normalizeShiftFormTimes(options.formData.value);

    const result = validateShiftFormData(options.formData.value, {
      timeZoneId: options.activeTimeZoneId.value,
      recurrenceError: recurrenceError.value,
      requireCancel: true,
    });

    if (!result.data) {
      formErrors.value = result.errors;
      return null;
    }

    return result.data;
  }

  async function saveShift() {
    const validated = validateForm();
    if (!validated) {
      return false;
    }

    const payload = buildUpdateShiftPayload({
      formData: validated,
      scope: options.selectedOpenScope.value === 'series' ? 'series' : 'entry',
      timeZoneId: options.activeTimeZoneId.value,
      locationId: options.activeLocationId.value,
      fallbackTitle: options.event.value.title,
      shiftSeriesId: resolveShiftSeriesId(options.event.value),
      existingRecurrenceRule: options.existingRecurrenceRule.value,
    });

    if (!payload) {
      apiError.value = 'Could not resolve the selected date and time.';
      return false;
    }

    isSaving.value = true;
    apiError.value = '';

    try {
      if (payload.cancel) {
        return payload.kind === 'series'
          ? await cancelShiftSeries(resolveShiftSeriesId(options.event.value), payload.cancel)
          : await cancelShiftEntry(resolveShiftEntryId(options.event.value), payload.cancel);
      }

      const saved =
        payload.kind === 'series'
          ? await updateShiftSeries(resolveShiftSeriesId(options.event.value), payload.body)
          : await updateShiftEntry(resolveShiftEntryId(options.event.value), payload.body);

      if (!saved) {
        return false;
      }

      return payload.kind === 'series'
        ? await publishShiftSeries(resolveShiftSeriesId(options.event.value), payload.publish)
        : await publishShiftEntry(resolveShiftEntryId(options.event.value), payload.publish);
    } catch (error: unknown) {
      apiError.value = error instanceof Error ? error.message : 'An unexpected error occurred.';
      return false;
    } finally {
      isSaving.value = false;
    }
  }

  async function updateShiftSeries(id: number | null, body: Parameters<typeof shiftApi.updateShiftSeries>[1]) {
    if (!id) {
      apiError.value = 'Could not determine the shift series to update.';
      return null;
    }

    const result = await shiftApi.updateShiftSeries(id, body);

    if (result.error.value) {
      if (applyServerValidationErrors(result.data.value)) {
        return null;
      }

      apiError.value = result.error.value.message || 'Failed to update shift series.';
      return null;
    }

    return result.data.value ?? null;
  }

  async function updateShiftEntry(id: number | null, body: Parameters<typeof shiftApi.updateShiftEntry>[1]) {
    if (!id) {
      apiError.value = 'Could not determine the shift entry to update.';
      return null;
    }

    const result = await shiftApi.updateShiftEntry(id, body);

    if (result.error.value) {
      if (applyServerValidationErrors(result.data.value)) {
        return null;
      }

      apiError.value = result.error.value.message || 'Failed to update shift entry.';
      return null;
    }

    return result.data.value ?? null;
  }

  async function publishShiftSeries(id: number | null, shouldPublish: boolean) {
    if (!shouldPublish || !id) {
      return true;
    }

    const publishResult = await shiftApi.publishShiftSeries(id);

    if (publishResult.error.value) {
      apiError.value = publishResult.error.value.message || 'Shift updated but failed to publish.';
      return false;
    }

    return true;
  }

  async function publishShiftEntry(id: number | null, shouldPublish: boolean) {
    if (!shouldPublish || !id) {
      return true;
    }

    const publishResult = await shiftApi.publishShiftEntry(id);

    if (publishResult.error.value) {
      apiError.value = publishResult.error.value.message || 'Shift updated but failed to publish.';
      return false;
    }

    return true;
  }

  async function cancelShiftSeries(id: number | null, shouldCancel: boolean) {
    if (!shouldCancel || !id) {
      return true;
    }

    const cancelResult = await shiftApi.cancelShiftSeries(id);

    if (cancelResult.error.value) {
      apiError.value = cancelResult.error.value.message || 'Shift updated but failed to cancel.';
      return false;
    }

    return true;
  }

  async function cancelShiftEntry(id: number | null, shouldCancel: boolean) {
    if (!shouldCancel || !id) {
      return true;
    }

    const cancelResult = await shiftApi.cancelShiftEntry(id);

    if (cancelResult.error.value) {
      apiError.value = cancelResult.error.value.message || 'Shift updated but failed to cancel.';
      return false;
    }

    return true;
  }

  function applyServerValidationErrors(rawError: unknown) {
    const mapped = mapToValidationErrors(rawError);
    if (!mapped) {
      return false;
    }

    formErrors.value = mapped;
    return true;
  }

  return {
    apiError,
    formErrors,
    isSaving,
    recurrenceError,
    clearErrors,
    handleRecurrenceChange,
    handleRecurrenceInvalid,
    saveShift,
  };
}
