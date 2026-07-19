<script setup lang="ts">
import { computed, ref, toRef, watch } from 'vue';
import type { CalendarMatrixResource } from '@/modules/calendar/components/matrix/calendarMatrixTypes';
import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import { useCalendarStore } from '@/modules/calendar/calendarStore';
import { formatCalendarEventTimeRange } from '@/utils/date';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaModal from '@/shared/components/UaModal.vue';
import { useLocationsStore } from '@/stores/LocationsStore';
import { mapToValidationErrors } from '@/shared/validation/validationErrors';
import { CalendarEventStatusTypeCode } from '@/api-access/generated/models';
import CalendarSchedulingShiftForm from './CalendarSchedulingShiftForm.vue';
import {
  buildCreateShiftPayloadWithErrors,
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
import { useSchedulingAssignmentOptions } from './useSchedulingAssignmentOptions';
import { useSchedulingEmployeeOptions } from './useSchedulingEmployeeOptions';
import type { SelectOption } from '@/types/select';

type ResourceModalTabId = 'schedule' | 'template' | 'loan' | 'time-off';

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

const modalTabs: Array<{ id: ResourceModalTabId; label: string }> = [
  { id: 'schedule', label: 'Schedule' },
  { id: 'template', label: 'Template' },
  { id: 'loan', label: 'Loan' },
  { id: 'time-off', label: 'Time Off' },
];

const activeTab = ref<ResourceModalTabId>('schedule');
const isSaving = ref(false);
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
const formLocationId = computed(() => normalizeLocationId(formData.value.locationId));
const { employeeOptions, isLoadingUsers } = useSchedulingEmployeeOptions(formLocationId, formData, {
  resource: toRef(props, 'resource'),
  onError: (message) => {
    apiError.value = message;
  },
});
const timeZoneId = computed(() => props.timeZone || Intl.DateTimeFormat().resolvedOptions().timeZone);
const isSeriesScope = computed(() => formData.value.repeatMode === 'custom' && Boolean(formData.value.recurrenceRule));
const { assignmentEntryOptions, assignmentSeriesOptions, assignmentWarning, isLoadingAssignments } =
  useSchedulingAssignmentOptions({
    formData,
    activeLocationId: formLocationId,
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

watch(
  () => [props.resource, props.initialDate, props.initialAssignmentEntryId, props.initialAssignmentEvents] as const,
  ([resource, initialDate]) => {
    formData.value = createInitialFormData(resource, initialDate);
    activeTab.value = 'schedule';
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

function createInitialFormData(
  resource: CalendarMatrixResource | undefined,
  initialDate?: string,
): ShiftResourceFormData {
  return {
    ...(resource
      ? createInitialShiftFormData(resource, activeLocationId.value, CalendarEventStatusTypeCode.Draft)
      : createInitialShiftFormDataForCreateAction(activeLocationId.value)),
    date: initialDate ?? '',
    assignmentEntryId: props.initialAssignmentEntryId ?? resolveInitialAssignmentEntryId(props.initialAssignmentEvents),
    assignmentEntryIds: resolveInitialAssignmentEntryIds(props.initialAssignmentEntryId, props.initialAssignmentEvents),
    assignmentEntryLinks: resolveInitialAssignmentEntryIds(
      props.initialAssignmentEntryId,
      props.initialAssignmentEvents,
    ).map((assignmentEntryId) => ({
      assignmentEntryId,
      assignedUserIds: resource?.type === 'user' ? [resource.id] : [],
    })),
  };
}

function normalizeLocationId(value: unknown) {
  const parsedLocationId = Number(value);
  return Number.isInteger(parsedLocationId) && parsedLocationId > 0 ? parsedLocationId : null;
}

function resolveInitialAssignmentEntryId(events?: CalendarEventBase[]) {
  const assignmentEntryIds = resolveInitialAssignmentEntryIds(undefined, events);
  return assignmentEntryIds.length === 1 ? assignmentEntryIds[0]! : null;
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
    if (!assignmentEntryId) {
      return [];
    }

    return [
      {
        code: assignmentEntryId,
        description: formatInitialAssignmentEventLabel(event),
      },
    ];
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

function selectTab(tabId: ResourceModalTabId) {
  activeTab.value = tabId;
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
  const validated = validateForm();
  if (!validated) {
    return;
  }

  const payloadResult = buildCreateShiftPayloadWithErrors({
    formData: validated,
    timeZoneId: timeZoneId.value,
    locationId: formLocationId.value,
    fallbackTitle: props.resource?.title || 'New',
  });
  if (!payloadResult.payload) {
    formErrors.value = {
      ...formErrors.value,
      ...payloadResult.errors,
    };
    apiError.value = payloadResult.errors.locationId
      ? 'A location is required before saving this shift.'
      : 'Could not build the shift request. Check the highlighted fields.';
    return;
  }

  const payload = payloadResult.payload;

  isSaving.value = true;
  apiError.value = '';

  try {
    const saveResult =
      payload.kind === 'series' ? await createShiftSeries(payload.body) : await createShiftEntry(payload.body);
    if (saveResult.error.value) {
      if (applyServerValidationErrors(saveResult.error.value.data)) {
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

    const published =
      payload.kind === 'series'
        ? await publishCreatedShiftSeries(saved.id, payload.publish)
        : await publishCreatedShiftEntry(saved.id, payload.publish);
    if (!published) {
      return;
    }

    calendarStore.refresh();
    emit('close');
  } catch (error: unknown) {
    apiError.value = error instanceof Error ? error.message : 'An unexpected error occurred.';
  } finally {
    isSaving.value = false;
  }
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

  formErrors.value = normalizeShiftServerValidationErrors(mapped);
  return true;
}

function normalizeShiftServerValidationErrors(errors: Record<string, string>) {
  return Object.entries(errors).reduce<Record<string, string>>((result, [fieldName, message]) => {
    result[mapShiftServerValidationField(fieldName)] = message;
    return result;
  }, {});
}

function mapShiftServerValidationField(fieldName: string) {
  const normalized = fieldName.toLowerCase();

  if (normalized === 'startatutc') {
    return 'startTime';
  }

  if (normalized === 'endatutc') {
    return 'endTime';
  }

  if (normalized === 'userid' || normalized === 'userids') {
    return 'userIds';
  }

  if (normalized === 'locationid') {
    return 'locationId';
  }

  if (normalized === 'assignmentseriesids' || normalized === 'assignmentserieslinks') {
    return 'assignmentSeriesId';
  }

  if (normalized === 'assignmententryids' || normalized === 'assignmententrylinks') {
    return 'assignmentEntryIds';
  }

  return fieldName;
}
</script>
<template>
  <UaModal :title="modalTitle" width="760" :loading="isSaving" @close="handleClose">
    <template #alerts>
      <UaAlert v-if="apiError" type="error" @close="apiError = ''">
        {{ apiError }}
      </UaAlert>
    </template>

    <template #secondary-header>
      <div class="resource-shift-modal__tabs" role="tablist" aria-label="New Shift Tabs">
        <button
          v-for="tab in modalTabs"
          :key="tab.id"
          :aria-selected="tab.id === activeTab"
          class="resource-shift-modal__tab"
          :class="{ 'resource-shift-modal__tab--active': tab.id === activeTab }"
          role="tab"
          type="button"
          @click="selectTab(tab.id)"
        >
          {{ tab.label }}
        </button>
      </div>
    </template>

    <div class="resource-shift-modal">
      <section v-if="activeTab === 'schedule'" class="resource-shift-modal__panel">
        <CalendarSchedulingShiftForm
          v-model="formData"
          id-prefix="new-shift"
          :form-errors="formErrors"
          :disabled="isSaving"
          :location-options="locationsStore.selectOptions"
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

      <section v-else class="resource-shift-modal__placeholder" aria-label="Inactive Tab Placeholder">
        <h3 class="resource-shift-modal__placeholder-heading">
          {{ modalTabs.find((tab) => tab.id === activeTab)?.label }}
        </h3>
        <p class="resource-shift-modal__placeholder-text">This section is not implemented yet.</p>
      </section>
    </div>

    <template #actions>
      <UaBtn variant="outlined" :disabled="isSaving" @click="handleClose">Cancel</UaBtn>
      <UaBtn color="primary" variant="flat" :loading="isSaving" @click="handleSave">Save</UaBtn>
    </template>
  </UaModal>
</template>

<style scoped>
.resource-shift-modal {
  display: grid;
  gap: var(--ua-spacing-lg);
}

.resource-shift-modal__tabs {
  display: flex;
  flex-wrap: wrap;
  gap: var(--ua-spacing-lg);
}

.resource-shift-modal__tab {
  background: transparent;
  border: 0;
  border-bottom: 2px solid transparent;
  color: var(--ua-text-primary);
  cursor: pointer;
  font-size: var(--ua-font-size-base);
  font-weight: var(--ua-font-weight-semibold);
  padding: 0 0 var(--ua-spacing-xs);
}

.resource-shift-modal__tab--active {
  border-bottom-color: rgb(var(--v-theme-primary));
}

.resource-shift-modal__panel,
.resource-shift-modal__placeholder {
  display: grid;
  gap: var(--ua-spacing-md);
}

.resource-shift-modal__employee-meta {
  grid-column: 2;
}

.resource-shift-modal__helper-text,
.resource-shift-modal__employee-id,
.resource-shift-modal__placeholder-text {
  color: var(--ua-text-secondary);
  font-size: var(--ua-font-size-sm);
  margin: 0;
}

.resource-shift-modal__field-error {
  color: rgb(var(--v-theme-error));
  font-size: var(--ua-font-size-sm);
  margin: var(--ua-spacing-xs) 0 0;
}

.resource-shift-modal__placeholder {
  border: 1px solid var(--ua-border-color);
  border-radius: var(--ua-border-radius);
  padding: var(--ua-spacing-lg);
}

.resource-shift-modal__placeholder-heading {
  color: var(--ua-text-primary);
  font-size: var(--ua-font-size-base);
  font-weight: var(--ua-font-weight-bold);
  margin: 0;
}

@media (max-width: 640px) {
  .resource-shift-modal__employee-meta {
    grid-column: auto;
  }
}
</style>
