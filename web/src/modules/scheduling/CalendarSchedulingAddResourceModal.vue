<script setup lang="ts">
import { computed, ref, toRef, watch } from 'vue';
import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import type { CalendarMatrixResource } from '@/modules/calendar/components/matrix/calendarMatrixTypes';
import { formatCalendarEventTimeRange } from '@/utils/date';
import { useCalendarStore } from '@/modules/calendar/calendarStore';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaModal from '@/shared/components/UaModal.vue';
import { useLocationsStore } from '@/stores/LocationsStore';
import { mapToValidationErrors } from '@/shared/validation/validationErrors';
import { CalendarEventStatusTypeCode } from '@/api-access/generated/models';
import type { ShiftEntryResponse } from '@/api-access/generated/models/shiftEntryResponse';
import type { ShiftSeriesResponse } from '@/api-access/generated/models/shiftSeriesResponse';
import CalendarSchedulingShiftForm from './CalendarSchedulingShiftForm.vue';
import {
  buildCreateShiftPayload,
  createInitialShiftFormData,
  createInitialShiftFormDataForCreateAction,
  normalizeShiftFormTimes,
  validateShiftFormData,
  type ShiftResourceFormData,
} from './calendarSchedulingShiftForm';
import {
  createShiftEntry,
  createShiftSeries,
  loadShiftEntries,
  loadShiftSeriesList,
  publishShiftEntry,
  publishShiftSeries,
} from './calendarSchedulingShiftApi';
import { useSchedulingEmployeeOptions } from './useSchedulingEmployeeOptions';
import { useSchedulingAssignmentOptions } from './useSchedulingAssignmentOptions';
import { syncCreatedShiftAssignmentLinks } from './calendarSchedulingCreatedShiftLinks';
import { resolveSchedulingTimeZoneId } from './schedulingTimeZone';
import type { SelectOption } from '@/types/select';
import { createLatestRequestGuard } from './latestRequestGuard';

const props = defineProps<{
  initialDate?: string;
  initialAssignmentEntryId?: number;
  initialAssignmentEvents?: CalendarEventBase[];
  resource?: CalendarMatrixResource;
  timeZone?: string;
}>();

const emit = defineEmits<{
  (event: 'close'): void;
}>();

const calendarStore = useCalendarStore();
const locationsStore = useLocationsStore();

const isSaving = ref(false);
const partiallySavedShiftId = ref<number | null>(null);
const hasPartiallySavedShift = computed(() => partiallySavedShiftId.value !== null);
const apiError = ref('');
const formErrors = ref<Record<string, string>>({});
const recurrenceError = ref('');
const existingShiftEntries = ref<ShiftEntryResponse[]>([]);
const existingShiftSeries = ref<ShiftSeriesResponse[]>([]);
const existingShiftsRequestGuard = createLatestRequestGuard();
const activeLocationId = computed<number | null>(() => {
  const candidate = locationsStore.selectedLocationId;

  if (candidate === '' || candidate == null) {
    return null;
  }

  const parsedLocationId = Number(candidate);
  return Number.isFinite(parsedLocationId) ? parsedLocationId : null;
});

const formData = ref<ShiftResourceFormData>(createInitialFormData(props.resource, props.initialDate));
const { employeeOptions, isLoadingUsers } = useSchedulingEmployeeOptions(activeLocationId, formData, {
  resource: toRef(props, 'resource'),
  onError: (message) => {
    apiError.value = message;
  },
});
const timeZoneId = computed(() =>
  resolveSchedulingTimeZoneId(
    activeLocationId.value ? locationsStore.entitiesMap[activeLocationId.value]?.timezone : undefined,
    props.timeZone,
  ),
);
const isSeriesScope = computed(() => formData.value.repeatMode === 'custom' && Boolean(formData.value.recurrenceRule));
const { assignmentEntryOptions, assignmentSeriesOptions, assignmentWarning, isLoadingAssignments } =
  useSchedulingAssignmentOptions({
    formData,
    activeLocationId,
    activeTimeZoneId: timeZoneId,
    isSeriesScope,
    onError: (message) => {
      apiError.value = message;
    },
  });
const seededAssignmentEntryOptions = computed(() => mapInitialAssignmentEventsToOptions(props.initialAssignmentEvents));
const assignmentEntryLabelsById = computed(() => {
  const labels = new Map<number, string>();

  for (const option of [...assignmentEntryOptions.value, ...seededAssignmentEntryOptions.value]) {
    if (typeof option.code === 'number' && option.description.trim()) {
      labels.set(option.code, option.description);
    }
  }

  return labels;
});
const mergedAssignmentEntryOptions = computed(() =>
  withSelectedAssignmentEntryOption(
    mergeSelectOptions(seededAssignmentEntryOptions.value, assignmentEntryOptions.value),
    formData.value.assignmentEntryLinks?.map((link) => link.assignmentEntryId).filter(isNumber),
    assignmentEntryLabelsById.value,
  ),
);
const modalTitle = computed(() => 'New Shift');
const locationOptions = computed(() => locationsStore.selectOptions);
const enteredLocationId = computed(() => {
  const parsed = Number(formData.value.locationId);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
});
const duplicateShiftWarning = computed(() => {
  const payload = buildCreateShiftPayload({
    formData: formData.value,
    timeZoneId: timeZoneId.value,
    locationId: enteredLocationId.value,
    fallbackTitle: props.resource?.title || 'New',
  });
  if (!payload) {
    return '';
  }

  const candidate = payload.body;
  const duplicateExists = [...existingShiftEntries.value, ...existingShiftSeries.value].some(
    (shift) =>
      shift.locationId === candidate.locationId &&
      representsSameInstant(shift.startAtUtc, candidate.startAtUtc) &&
      representsSameInstant(shift.endAtUtc, candidate.endAtUtc),
  );

  return duplicateExists ? 'Shift with selected Date/Time already exists.' : '';
});

watch(
  () => [props.resource, props.initialDate, props.initialAssignmentEntryId, props.initialAssignmentEvents] as const,
  ([resource, initialDate]) => {
    formData.value = createInitialFormData(resource, initialDate);
    apiError.value = '';
    recurrenceError.value = '';
    formErrors.value = {};
  },
);

watch(
  () => formData.value.repeatMode,
  (value) => {
    if (value === 'never') {
      formData.value.recurrenceRule = null;
      recurrenceError.value = '';
    }
  },
);

watch(activeLocationId, (locationId) => {
  if (formData.value.locationId == null && locationId != null) {
    formData.value = {
      ...formData.value,
      locationId,
    };
  }
});

watch(
  activeLocationId,
  async (locationId) => {
    const requestId = existingShiftsRequestGuard.begin();
    existingShiftEntries.value = [];
    existingShiftSeries.value = [];
    if (locationId == null) {
      return;
    }

    try {
      const userParams = props.resource?.type === 'user' ? { UserId: props.resource.id } : undefined;
      const [entriesResult, seriesResult] = await Promise.all([
        loadShiftEntries(userParams),
        loadShiftSeriesList(userParams),
      ]);
      if (!existingShiftsRequestGuard.isCurrent(requestId)) {
        return;
      }

      existingShiftEntries.value = entriesResult.error.value ? [] : (entriesResult.data.value ?? []);
      existingShiftSeries.value = seriesResult.error.value ? [] : (seriesResult.data.value ?? []);
    } catch {
      if (existingShiftsRequestGuard.isCurrent(requestId)) {
        existingShiftEntries.value = [];
        existingShiftSeries.value = [];
      }
    }
  },
  { immediate: true },
);

function representsSameInstant(left: string | null | undefined, right: string | null | undefined) {
  if (!left || !right) {
    return false;
  }

  const leftMillis = Date.parse(left);
  const rightMillis = Date.parse(right);
  return Number.isFinite(leftMillis) && Number.isFinite(rightMillis) && leftMillis === rightMillis;
}

function createInitialFormData(
  resource: CalendarMatrixResource | undefined,
  initialDate?: string,
): ShiftResourceFormData {
  const assignmentEntryIds = resolveInitialAssignmentEntryIds(
    props.initialAssignmentEntryId,
    props.initialAssignmentEvents,
  );

  return {
    ...(resource
      ? createInitialShiftFormData(resource, activeLocationId.value, CalendarEventStatusTypeCode.Draft)
      : createInitialShiftFormDataForCreateAction(activeLocationId.value)),
    date: initialDate ?? '',
    assignmentEntryId: assignmentEntryIds.length === 1 ? assignmentEntryIds[0] : null,
    assignmentEntryIds,
    assignmentEntryLinks: assignmentEntryIds.map((assignmentEntryId) => ({
      assignmentEntryId,
      assignedUserIds: resource?.type === 'user' ? [resource.id] : [],
    })),
  };
}

function resolveInitialAssignmentEntryIds(initialAssignmentEntryId?: number, events?: CalendarEventBase[]) {
  return [
    ...new Set([
      ...(initialAssignmentEntryId ? [initialAssignmentEntryId] : []),
      ...(events ?? []).map(resolveAssignmentEntryId).filter(isNumber),
    ]),
  ];
}

function mapInitialAssignmentEventsToOptions(events?: CalendarEventBase[]) {
  return (events ?? []).flatMap((event) => {
    const assignmentEntryId = resolveAssignmentEntryId(event);
    return assignmentEntryId
      ? [{ code: assignmentEntryId, description: formatInitialAssignmentEventLabel(event) }]
      : [];
  });
}

function mergeSelectOptions(primary: SelectOption[], secondary: SelectOption[]): SelectOption[] {
  const options = new Map<SelectOption['code'], SelectOption>();

  for (const option of [...primary, ...secondary]) {
    if (!options.has(option.code)) {
      options.set(option.code, option);
    }
  }

  return Array.from(options.values());
}

function withSelectedAssignmentEntryOption(
  options: SelectOption[],
  assignmentEntryIds: number[] | undefined | null,
  labelsById: Map<number, string>,
) {
  const existingCodes = new Set(options.map((option) => option.code));
  const fallbackOptions = (assignmentEntryIds ?? [])
    .filter((assignmentEntryId) => !existingCodes.has(assignmentEntryId))
    .map((assignmentEntryId) => ({
      code: assignmentEntryId,
      description: labelsById.get(assignmentEntryId) ?? `Assignment ${assignmentEntryId}`,
    }));

  return [...fallbackOptions, ...options];
}

function resolveAssignmentEntryId(event: CalendarEventBase) {
  const metadata = (event as { metadata?: { assignmentEntryId?: unknown } }).metadata;
  const parsed = Number(metadata?.assignmentEntryId);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function isNumber(value: number | null | undefined): value is number {
  return typeof value === 'number';
}

function formatInitialAssignmentEventLabel(event: CalendarEventBase) {
  const title = event.title?.trim() || 'Assignment';
  const timeRange = formatCalendarEventTimeRange(event.start, event.end, {
    allDay: event.allDay,
    timeZone: event.timeZoneId ?? timeZoneId.value,
  });

  return timeRange ? `${title} (${timeRange})` : title;
}

function handleClose() {
  if (!isSaving.value) {
    emit('close');
  }
}

function handleRecurrenceInvalid(reason: string) {
  recurrenceError.value = reason;
}

function handleRecurrenceChange(value: string | null) {
  recurrenceError.value = '';
  formData.value.recurrenceRule = value;
}

function validateForm(): ShiftResourceFormData | null {
  formErrors.value = {};
  formData.value = normalizeShiftFormTimes(formData.value);

  const result = validateShiftFormData(formData.value, {
    timeZoneId: timeZoneId.value,
    recurrenceError: recurrenceError.value,
  });

  if (!result.data) {
    formErrors.value = result.errors;
    return null;
  }

  return result.data;
}

async function handleSave() {
  if (partiallySavedShiftId.value) {
    apiError.value = 'This shift was already saved. Close and reopen it before retrying relationship changes.';
    return;
  }

  const validated = validateForm();
  if (!validated) {
    apiError.value = 'Could not save the shift. Check the highlighted fields.';
    return;
  }

  const payload = buildCreateShiftPayload({
    formData: validated,
    timeZoneId: timeZoneId.value,
    locationId: validated.locationId ?? null,
    fallbackTitle: props.resource?.title || 'New',
  });
  if (!payload) {
    apiError.value = 'Could not resolve the selected date and time.';
    return;
  }

  isSaving.value = true;
  apiError.value = '';
  let savedId: number | undefined;

  try {
    const saveResult =
      payload.kind === 'series' ? await createShiftSeries(payload.body) : await createShiftEntry(payload.body);
    if (saveResult.error.value) {
      if (applyServerValidationErrors(saveResult.data.value)) {
        return;
      }

      apiError.value =
        saveResult.error.value.message ||
        (payload.kind === 'series' ? 'Failed to create shift series.' : 'Failed to create shift entry.');
      return;
    }

    const saved = saveResult.data.value;
    if (!saved?.id) {
      apiError.value = 'Shift was created but the response did not include an id.';
      return;
    }
    savedId = saved.id;

    await syncCreatedShiftAssignmentLinks(payload.kind, saved.id, validated);

    const published =
      payload.kind === 'series'
        ? await publishCreatedShiftSeries(saved.id, payload.publish)
        : await publishCreatedShiftEntry(saved.id, payload.publish);
    if (!published) {
      recoverPartiallySavedShift(
        saved.id,
        'The shift was saved as Draft, but publication failed. Close this dialog and reopen the shift to continue.',
      );
      return;
    }

    calendarStore.refresh();
    emit('close');
  } catch (error: unknown) {
    if (savedId) {
      const reason = error instanceof Error ? error.message : 'relationships could not be fully updated';
      recoverPartiallySavedShift(
        savedId,
        `The shift was created, but some relationships could not be saved: ${reason}. Close this dialog and reopen the shift to continue editing.`,
      );
    } else {
      apiError.value = error instanceof Error ? error.message : 'An unexpected error occurred.';
    }
  } finally {
    isSaving.value = false;
  }
}

function recoverPartiallySavedShift(savedId: number, message: string) {
  partiallySavedShiftId.value = savedId;
  calendarStore.refresh();
  apiError.value = message;
}

async function publishCreatedShiftSeries(id: number | undefined, shouldPublish: boolean) {
  if (!shouldPublish || !id) {
    return true;
  }

  const publishResult = await publishShiftSeries(id);

  if (publishResult.error.value) {
    apiError.value = publishResult.error.value.message || 'Shift created but failed to publish.';
    return false;
  }

  return true;
}

async function publishCreatedShiftEntry(id: number | undefined, shouldPublish: boolean) {
  if (!shouldPublish || !id) {
    return true;
  }

  const publishResult = await publishShiftEntry(id);

  if (publishResult.error.value) {
    apiError.value = publishResult.error.value.message || 'Shift created but failed to publish.';
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
</script>
<template>
  <UaModal :title="modalTitle" width="760" :loading="isSaving" @close="handleClose">
    <template #alerts>
      <UaAlert v-if="apiError" type="error" @close="apiError = ''">
        {{ apiError }}
      </UaAlert>
      <UaAlert v-if="duplicateShiftWarning" type="warning" :closable="false">
        {{ duplicateShiftWarning }}
      </UaAlert>
    </template>

    <div class="resource-shift-modal">
      <section class="resource-shift-modal__panel">
        <CalendarSchedulingShiftForm
          v-model="formData"
          id-prefix="new-shift"
          :form-errors="formErrors"
          :disabled="isSaving || hasPartiallySavedShift"
          :location-options="locationOptions"
          :employee-options="employeeOptions"
          :is-loading-users="isLoadingUsers"
          :assignment-entry-options="mergedAssignmentEntryOptions"
          :assignment-series-options="assignmentSeriesOptions"
          :assignment-warning="assignmentWarning"
          :is-loading-assignments="isLoadingAssignments"
          :show-series-assignment="isSeriesScope"
          @recurrence-change="handleRecurrenceChange"
          @recurrence-invalid="handleRecurrenceInvalid"
        />
      </section>
    </div>

    <template #actions>
      <UaBtn variant="outlined" :disabled="isSaving" @click="handleClose">
        {{ hasPartiallySavedShift ? 'Close' : 'Cancel' }}
      </UaBtn>
      <UaBtn v-if="!hasPartiallySavedShift" color="primary" variant="flat" :loading="isSaving" @click="handleSave">
        Save
      </UaBtn>
    </template>
  </UaModal>
</template>

<style scoped>
.resource-shift-modal {
  display: grid;
  gap: var(--ua-spacing-lg);
}

.resource-shift-modal__panel {
  display: grid;
  gap: var(--ua-spacing-md);
}

.resource-shift-modal__employee-meta {
  grid-column: 2;
}

.resource-shift-modal__helper-text,
.resource-shift-modal__employee-id {
  color: var(--ua-text-secondary);
  font-size: var(--ua-font-size-sm);
  margin: 0;
}

.resource-shift-modal__field-error {
  color: rgb(var(--v-theme-error));
  font-size: var(--ua-font-size-sm);
  margin: var(--ua-spacing-xs) 0 0;
}

@media (max-width: 640px) {
  .resource-shift-modal__employee-meta {
    grid-column: auto;
  }
}
</style>
