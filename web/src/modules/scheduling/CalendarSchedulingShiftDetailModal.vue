<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import { useCalendarStore } from '@/modules/calendar/calendarStore';
import { useLocationsStore } from '@/stores/LocationsStore';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaModal from '@/shared/components/UaModal.vue';
import CalendarSchedulingShiftDeletePanel from './CalendarSchedulingShiftDeletePanel.vue';
import CalendarSchedulingShiftDetailsPanel from './CalendarSchedulingShiftDetailsPanel.vue';
import CalendarSchedulingShiftEditPanel from './CalendarSchedulingShiftEditPanel.vue';
import {
  createShiftFormDataFromEvent,
  createShiftFormDataFromSeries,
  type ShiftResourceFormData,
} from './calendarSchedulingShiftForm';
import type { ShiftDetailTabId, ShiftOpenScope } from './calendarSchedulingShiftDetailTypes';
import { resolveShiftSeriesId } from './calendarSchedulingShiftIds';
import { useSchedulingEmployeeOptions } from './useSchedulingEmployeeOptions';
import { useSchedulingShiftDelete } from './useSchedulingShiftDelete';
import { useSchedulingShiftDetailRows } from './useSchedulingShiftDetailRows';
import { useSchedulingShiftMutation } from './useSchedulingShiftMutation';
import { useSchedulingShiftSeriesLoader } from './useSchedulingShiftSeriesLoader';

const props = defineProps<{
  event: CalendarEventBase;
}>();

const emit = defineEmits<{
  (event: 'close'): void;
}>();

const calendarStore = useCalendarStore();
const locationsStore = useLocationsStore();

const tabs: Array<{ id: ShiftDetailTabId; label: string }> = [
  { id: 'details', label: 'Details' },
  { id: 'edit', label: 'Edit' },
  { id: 'transfer', label: 'Transfer' },
  { id: 'copy', label: 'Copy' },
  { id: 'delete', label: 'Delete' },
];

const activeTab = ref<ShiftDetailTabId>('details');
const selectedOpenScope = ref<ShiftOpenScope | null>(getInitialOpenScope());
const placeholderNotice = ref('');
const modalError = ref('');
const eventRef = computed(() => props.event);
const timeZoneId = computed(() => props.event.timeZoneId || Intl.DateTimeFormat().resolvedOptions().timeZone);
const editFormData = ref<ShiftResourceFormData>(createEditFormData());

const { selectedSeries, isLoadingSeries, loadSelectedSeries, clearSelectedSeries } = useSchedulingShiftSeriesLoader({
  event: eventRef,
  onError: setApiError,
});

const activeTimeZoneId = computed(() =>
  selectedOpenScope.value === 'series' ? selectedSeries.value?.timeZoneId || timeZoneId.value : timeZoneId.value,
);
const activeLocationId = computed<number | null>(() => {
  if (selectedOpenScope.value === 'series' && selectedSeries.value?.locationId != null) {
    return selectedSeries.value.locationId;
  }

  if (props.event.locationId != null) {
    return props.event.locationId;
  }

  const candidate = locationsStore.selectedLocationId;

  if (candidate === '' || candidate == null) {
    return null;
  }

  const parsedLocationId = Number(candidate);
  return Number.isFinite(parsedLocationId) ? parsedLocationId : null;
});
const eventBelongsToSeries = computed(() => resolveShiftSeriesId(props.event) !== null);
const shouldShowOpenScopeChoice = computed(() => eventBelongsToSeries.value && selectedOpenScope.value === null);
const isSeriesScope = computed(() => selectedOpenScope.value === 'series');
const modalTitle = computed(() => (isSeriesScope.value ? 'Shift Series Details' : 'Shift Details'));

const { employeeOptions, isLoadingUsers } = useSchedulingEmployeeOptions(activeLocationId, editFormData, {
  onError: setApiError,
});

const {
  apiError: mutationError,
  formErrors,
  isSaving: isMutating,
  clearErrors: clearMutationErrors,
  handleRecurrenceChange,
  handleRecurrenceInvalid,
  saveShift,
} = useSchedulingShiftMutation({
  event: eventRef,
  formData: editFormData,
  selectedOpenScope,
  activeTimeZoneId,
  activeLocationId,
  existingRecurrenceRule: computed(() => selectedSeries.value?.recurrenceRule ?? null),
});

const {
  canDeleteShift,
  deleteDisabledReason,
  deleteError,
  isDeleteConfirmed,
  isDeleting,
  clearDeleteState,
  deleteShift,
} = useSchedulingShiftDelete({
  event: eventRef,
  selectedOpenScope,
  selectedSeries,
});

const { detailRows } = useSchedulingShiftDetailRows({
  event: eventRef,
  selectedOpenScope,
  selectedSeries,
  employeeOptions,
  activeTimeZoneId,
});
const apiError = computed(() => modalError.value || mutationError.value || deleteError.value);
const isSaving = computed(() => isMutating.value || isDeleting.value);

watch(
  () => props.event,
  (event) => {
    clearSelectedSeries();
    editFormData.value = createShiftFormDataFromEvent(event, timeZoneId.value);
    activeTab.value = 'details';
    selectedOpenScope.value = getInitialOpenScope();
    placeholderNotice.value = '';
    clearApiError();
  },
);

watch(
  () => editFormData.value.repeatMode,
  (value) => {
    if (value === 'never') {
      editFormData.value.recurrenceRule = null;
      handleRecurrenceChange(null);
    }
  },
);

const placeholderHeading = computed(() => {
  switch (activeTab.value) {
    case 'edit':
      return 'Edit shift';
    case 'transfer':
      return 'Transfer shift';
    case 'copy':
      return 'Copy shift';
    case 'delete':
      return 'Delete shift';
    default:
      return 'Shift details';
  }
});

const placeholderDescription = computed(() => {
  switch (activeTab.value) {
    case 'edit':
      return 'Editing is not implemented yet.';
    case 'transfer':
      return 'Transfer is not implemented yet.';
    case 'copy':
      return 'Copy is not implemented yet.';
    case 'delete':
      return 'Delete is not implemented yet. This tab is a placeholder for the future delete flow.';
    default:
      return '';
  }
});

function selectTab(tabId: ShiftDetailTabId) {
  activeTab.value = tabId;
  placeholderNotice.value = '';
  clearApiError();
  clearDeleteState();
}

async function selectOpenScope(scope: ShiftOpenScope) {
  clearApiError();
  placeholderNotice.value = '';

  if (scope === 'series') {
    const series = await loadSelectedSeries();
    if (!series) {
      return;
    }
  } else {
    clearSelectedSeries();
  }

  selectedOpenScope.value = scope;
  editFormData.value = createEditFormData();
  activeTab.value = 'details';
}

function createEditFormData(): ShiftResourceFormData {
  if (selectedOpenScope.value === 'series' && selectedSeries.value) {
    return createShiftFormDataFromSeries(selectedSeries.value, props.event, activeTimeZoneId.value);
  }

  return createShiftFormDataFromEvent(props.event, timeZoneId.value);
}

async function handleSaveEdit() {
  const saved = await saveShift();

  if (saved) {
    calendarStore.refresh();
    emit('close');
  }
}

async function handleDeleteShift() {
  const deleted = await deleteShift();

  if (deleted) {
    calendarStore.refresh();
    emit('close');
  }
}

function getInitialOpenScope(): ShiftOpenScope | null {
  return resolveShiftSeriesId(props.event) ? null : 'event';
}

function setApiError(message: string) {
  modalError.value = message;
}

function clearApiError() {
  modalError.value = '';
  clearMutationErrors();
  clearDeleteState();
}
</script>

<template>
  <UaModal :title="modalTitle" width="760" @close="emit('close')">
    <template #alerts>
      <UaAlert v-if="apiError" type="error" @close="clearApiError">
        {{ apiError }}
      </UaAlert>
      <UaAlert v-if="placeholderNotice" type="info" @close="placeholderNotice = ''">
        {{ placeholderNotice }}
      </UaAlert>
    </template>

    <div v-if="shouldShowOpenScopeChoice" class="shift-detail-modal__scope-choice">
      <p class="shift-detail-modal__scope-choice-text">This is one event in a series, What do you want to open?</p>
      <div class="shift-detail-modal__scope-choice-actions">
        <UaBtn variant="outlined" :disabled="isLoadingSeries" @click="selectOpenScope('event')">Only this event</UaBtn>
        <UaBtn color="primary" variant="flat" :loading="isLoadingSeries" @click="selectOpenScope('series')">
          The entire series
        </UaBtn>
      </div>
    </div>

    <div v-else class="shift-detail-modal">
      <div class="shift-detail-modal__tabs" role="tablist" aria-label="Shift detail tabs">
        <button
          v-for="tab in tabs"
          :key="tab.id"
          :aria-selected="tab.id === activeTab"
          class="shift-detail-modal__tab"
          :class="{ 'shift-detail-modal__tab--active': tab.id === activeTab }"
          role="tab"
          type="button"
          @click="selectTab(tab.id)"
        >
          {{ tab.label }}
        </button>
      </div>

      <CalendarSchedulingShiftDetailsPanel v-if="activeTab === 'details'" :detail-rows="detailRows" />

      <CalendarSchedulingShiftEditPanel
        v-else-if="activeTab === 'edit'"
        v-model="editFormData"
        :form-errors="formErrors"
        :disabled="isSaving"
        :show-recurrence="isSeriesScope"
        :employee-options="employeeOptions"
        :is-loading-users="isLoadingUsers"
        @recurrence-change="handleRecurrenceChange"
        @recurrence-invalid="handleRecurrenceInvalid"
      />

      <CalendarSchedulingShiftDeletePanel
        v-else-if="activeTab === 'delete'"
        v-model:is-delete-confirmed="isDeleteConfirmed"
        :detail-rows="detailRows"
        :delete-disabled-reason="deleteDisabledReason"
      />

      <section v-else class="shift-detail-modal__panel" :aria-label="`${placeholderHeading} panel`">
        <div class="shift-detail-modal__placeholder">
          <h3 class="shift-detail-modal__placeholder-heading">{{ placeholderHeading }}</h3>
          <p class="shift-detail-modal__placeholder-text">{{ placeholderDescription }}</p>
        </div>
      </section>
    </div>

    <template v-if="!shouldShowOpenScopeChoice && (activeTab === 'edit' || activeTab === 'delete')" #actions>
      <template v-if="activeTab === 'edit'">
        <UaBtn variant="outlined" :disabled="isSaving" @click="selectTab('details')">Cancel</UaBtn>
        <UaBtn color="primary" variant="flat" :loading="isSaving" @click="handleSaveEdit">Save</UaBtn>
      </template>
      <template v-else>
        <UaBtn variant="outlined" :disabled="isSaving" @click="emit('close')">Close</UaBtn>
        <UaBtn color="error" variant="flat" :disabled="!canDeleteShift" :loading="isSaving" @click="handleDeleteShift">
          Delete
        </UaBtn>
      </template>
    </template>
  </UaModal>
</template>

<style scoped>
.shift-detail-modal {
  display: grid;
  gap: var(--ua-spacing-lg);
}

.shift-detail-modal__scope-choice {
  display: grid;
  gap: var(--ua-spacing-lg);
}

.shift-detail-modal__scope-choice-text {
  color: var(--ua-text-primary);
  font-size: var(--ua-font-size-base);
  font-weight: var(--ua-font-weight-semibold);
  margin: 0;
}

.shift-detail-modal__scope-choice-actions {
  display: flex;
  flex-wrap: wrap;
  gap: var(--ua-spacing-md);
  justify-content: flex-end;
}

.shift-detail-modal__tabs {
  display: flex;
  flex-wrap: wrap;
  gap: var(--ua-spacing-lg);
}

.shift-detail-modal__tab {
  background: transparent;
  border: 0;
  border-bottom: 2px solid transparent;
  color: var(--ua-text-primary);
  cursor: pointer;
  font-size: var(--ua-font-size-base);
  font-weight: var(--ua-font-weight-semibold);
  padding: 0 0 var(--ua-spacing-xs);
}

.shift-detail-modal__tab--active {
  border-bottom-color: rgb(var(--v-theme-primary));
}

.shift-detail-modal__placeholder {
  border: 1px solid var(--ua-border-color);
  border-radius: var(--ua-border-radius);
  display: grid;
  gap: var(--ua-spacing-sm);
  padding: var(--ua-spacing-lg);
}

.shift-detail-modal__placeholder-heading {
  color: var(--ua-text-primary);
  font-size: var(--ua-font-size-base);
  font-weight: var(--ua-font-weight-bold);
  margin: 0;
}

.shift-detail-modal__placeholder-text {
  color: var(--ua-text-secondary);
  font-size: var(--ua-font-size-sm);
  margin: 0;
}
</style>
