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
  publishShiftEntry,
  publishShiftSeries,
} from './calendarSchedulingShiftApi';
import { useSchedulingEmployeeOptions } from './useSchedulingEmployeeOptions';
import { useSchedulingAssignmentOptions } from './useSchedulingAssignmentOptions';
import { resolveSchedulingTimeZoneId } from './schedulingTimeZone';
import type { SelectOption } from '@/types/select';
import { Permissions } from '@/api-access/generated/models';
import { useAccessControl } from '@/composables/useAccessControl';

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
const accessControl = useAccessControl();
const canCreateShift = computed(() => accessControl.hasPermission(Permissions.ShiftsCreateAndAssign));

const isSaving = ref(false);
const createdShiftId = ref<number | null>(null);
const hasCreatedUnpublishedShift = computed(() => createdShiftId.value !== null);
const apiError = ref('');
const formErrors = ref<Record<string, string>>({});
const recurrenceError = ref('');
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

watch(
  () => [props.resource, props.initialDate, props.initialAssignmentEntryId, props.initialAssignmentEvents] as const,
  ([resource, initialDate]) => {
    formData.value = createInitialFormData(resource, initialDate);
    createdShiftId.value = null;
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
  if (!canCreateShift.value) {
    apiError.value = 'You do not have permission to create shifts.';
    return;
  }

  if (hasCreatedUnpublishedShift.value) {
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
    if (payload.publish && !saved?.id) {
      apiError.value = 'Shift was created but the response did not include an id.';
      return;
    }
    createdShiftId.value = payload.publish ? (saved?.id ?? null) : null;
    const published =
      payload.kind === 'series'
        ? await publishCreatedShiftSeries(saved?.id, payload.publish)
        : await publishCreatedShiftEntry(saved?.id, payload.publish);
    if (!published) {
      handlePublicationFailure();
      return;
    }

    calendarStore.refresh();
    emit('close');
  } catch (error: unknown) {
    if (hasCreatedUnpublishedShift.value) {
      handlePublicationFailure();
    } else {
      apiError.value = error instanceof Error ? error.message : 'An unexpected error occurred.';
    }
  } finally {
    isSaving.value = false;
  }
}

function handlePublicationFailure() {
  calendarStore.refresh();
  apiError.value =
    'The shift was created and remains Draft, but it could not be published. Close and reopen it to continue.';
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
  if (mapped.userIds) {
    apiError.value = 'One or more selected employees already have a shift at this location on the selected date.';
  }
  return true;
}
</script>
<template>
  <UaModal :title="modalTitle" width="760" :loading="isSaving" @close="handleClose">
    <template #alerts>
      <UaAlert v-if="apiError" type="error" @close="apiError = ''">
        {{ apiError }}
      </UaAlert>
    </template>

    <div class="resource-shift-modal">
      <section class="resource-shift-modal__panel">
        <CalendarSchedulingShiftForm
          v-model="formData"
          id-prefix="new-shift"
          :form-errors="formErrors"
          :disabled="isSaving || hasCreatedUnpublishedShift || !canCreateShift"
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
        {{ hasCreatedUnpublishedShift ? 'Close' : 'Cancel' }}
      </UaBtn>
      <UaBtn
        v-if="!hasCreatedUnpublishedShift && canCreateShift"
        color="primary"
        variant="flat"
        :loading="isSaving"
        @click="handleSave"
      >
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
