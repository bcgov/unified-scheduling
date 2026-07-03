<script setup lang="ts">
import { computed, ref, toRef, watch } from 'vue';
import type { CalendarMatrixResource } from '@/modules/calendar/components/matrix/calendarMatrixTypes';
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

type ResourceModalTabId = 'schedule' | 'template' | 'loan' | 'time-off';

const props = defineProps<{
  initialDate?: string;
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
  { id: 'time-off', label: 'Time off' },
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
const { employeeOptions, isLoadingUsers } = useSchedulingEmployeeOptions(activeLocationId, formData, {
  resource: toRef(props, 'resource'),
  onError: (message) => {
    apiError.value = message;
  },
});
const timeZoneId = computed(() => props.timeZone || Intl.DateTimeFormat().resolvedOptions().timeZone);
const modalTitle = computed(() => 'New Shift');

watch(
  () => [props.resource, props.initialDate] as const,
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
  };
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

  const payload = buildCreateShiftPayload({
    formData: validated,
    timeZoneId: timeZoneId.value,
    locationId: activeLocationId.value,
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
    </template>

    <template #secondary-header>
      <div class="resource-shift-modal__tabs" role="tablist" aria-label="New shift tabs">
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
          :employee-options="employeeOptions"
          :is-loading-users="isLoadingUsers"
          @recurrence-change="handleRecurrenceChange"
          @recurrence-invalid="handleRecurrenceInvalid"
        />
      </section>

      <section v-else class="resource-shift-modal__placeholder" :aria-label="`${activeTab} tab placeholder`">
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
